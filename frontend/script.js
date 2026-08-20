const API_BASE = "";

let pollInterval = null;

async function fetchAttendees() {
    try {
        const res = await fetch(`${API_BASE}/api/attendees`);
        if (!res.ok) throw new Error("Failed to load attendees");
        const attendees = await res.json();
        renderAttendees(attendees);
    } catch (err) {
        console.error("Error fetching attendees:", err);
    }
}

async function fetchPrintJobs() {
    try {
        const res = await fetch(`${API_BASE}/api/print-jobs`);
        if (!res.ok) return;
        const jobs = await res.json();
        renderPrintJobs(jobs);
    } catch (err) {
        console.error("Error fetching print jobs:", err);
    }
}

function renderAttendees(attendees) {
    const grid = document.getElementById("attendeeGrid");
    grid.innerHTML = "";

    attendees.forEach(a => {
        const card = document.createElement("div");
        card.className = "attendee-card";

        let badgeClass = "not-checked-in";
        let badgeText = "Ready to Check In";
        let buttonText = "📷 Scan Badge QR";
        let buttonClass = "btn-primary";
        let disabled = false;

        if (a.status === "PENDING") {
            badgeClass = "pending";
            badgeText = "⏳ Printing...";
            buttonText = "⏳ Printing in Progress";
            buttonClass = "btn-warning";
            disabled = true;
        } else if (a.status === "CHECKED_IN") {
            badgeClass = "checked-in";
            badgeText = "✓ Checked In";
            buttonText = "✓ Already Checked In";
            buttonClass = "btn-success";
            disabled = true;
        }

        card.innerHTML = `
            <div class="attendee-info">
                <div>
                    <div class="attendee-name">${a.name}</div>
                    <div class="attendee-id">${a.id}</div>
                </div>
                <span class="badge ${badgeClass}">${badgeText}</span>
            </div>
            <button class="btn ${buttonClass}" ${disabled ? "disabled" : ""} onclick="scanAttendee('${a.id}')">
                ${buttonText}
            </button>
        `;
        grid.appendChild(card);
    });

    // If any attendee is PENDING, ensure fast polling is active
    const hasPending = attendees.some(a => a.status === "PENDING");
    if (hasPending && !pollInterval) {
        pollInterval = setInterval(() => {
            fetchAttendees();
            fetchPrintJobs();
        }, 1000);
    } else if (!hasPending && pollInterval) {
        clearInterval(pollInterval);
        pollInterval = null;
    }
}

function renderPrintJobs(jobs) {
    const tbody = document.getElementById("printJobsTable");
    const countBadge = document.getElementById("jobCount");
    countBadge.textContent = `${jobs.length} Jobs`;

    if (jobs.length === 0) {
        tbody.innerHTML = `<tr><td colspan="5" class="empty-state">No print jobs yet. Scan an attendee to start.</td></tr>`;
        return;
    }

    tbody.innerHTML = jobs.map(j => `
        <tr>
            <td><code>${j.id}</code></td>
            <td><strong>${j.attendeeId}</strong></td>
            <td><span class="badge ${j.status === 'completed' ? 'checked-in' : 'pending'}">${j.status}</span></td>
            <td>${new Date(j.createdAt).toLocaleTimeString()}</td>
            <td>${j.completedAt ? new Date(j.completedAt).toLocaleTimeString() : '<em>In progress...</em>'}</td>
        </tr>
    `).join("");
}

async function scanAttendee(attendeeId) {
    showNotification(`Initiating check-in for attendee ${attendeeId}...`, "info");
    try {
        const res = await fetch(`${API_BASE}/api/checkin/${attendeeId}`, {
            method: "POST",
            headers: { "Content-Type": "application/json" }
        });

        const data = await res.json();
        if (res.ok) {
            if (data.status === "PENDING") {
                showNotification(`✓ ${data.message} (State: PENDING)`, "warning");
            } else if (data.status === "CHECKED_IN") {
                showNotification(`ℹ️ ${data.message}`, "info");
            }
            await fetchAttendees();
            await fetchPrintJobs();

            // Start polling for asynchronous completion
            if (!pollInterval) {
                pollInterval = setInterval(() => {
                    fetchAttendees();
                    fetchPrintJobs();
                }, 1000);
            }
        } else {
            showNotification(`Error: ${data.message}`, "warning");
        }
    } catch (err) {
        showNotification(`Network error: ${err.message}`, "warning");
    }
}

async function testDuplicateWebhook() {
    const fixedEventId = `evt-demo-duplicate-${Date.now()}`;
    const payload = {
        eventId: fixedEventId,
        printJobId: "job-demo-test",
        attendeeId: "A001",
        status: "completed"
    };

    showNotification(`Sending First Webhook event [${fixedEventId}]...`, "info");
    const res1 = await fetch(`${API_BASE}/api/webhooks/print-completed`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
    });
    const data1 = await res1.json();

    showNotification(`1st Webhook Response: isDuplicate=${data1.isDuplicate}, Message="${data1.message}". Now sending 2nd duplicate...`, "info");

    await new Promise(r => setTimeout(r, 600));

    const res2 = await fetch(`${API_BASE}/api/webhooks/print-completed`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
    });
    const data2 = await res2.json();

    showNotification(`✓ 2nd Webhook Response: isDuplicate=${data2.isDuplicate} (Safely Ignored via SQLite Webhook Idempotency!)`, "success");
    await fetchAttendees();
    await fetchPrintJobs();
}

async function resetSystem() {
    try {
        const res = await fetch(`${API_BASE}/api/reset`, { method: "POST" });
        const data = await res.json();
        showNotification(data.message, "success");
        if (pollInterval) {
            clearInterval(pollInterval);
            pollInterval = null;
        }
        await fetchAttendees();
        await fetchPrintJobs();
    } catch (err) {
        showNotification(`Reset failed: ${err.message}`, "warning");
    }
}

function showNotification(msg, type = "info") {
    const el = document.getElementById("notification");
    el.textContent = msg;
    el.className = `notification ${type}`;
    el.classList.remove("hidden");
    setTimeout(() => {
        el.classList.add("hidden");
    }, 6000);
}

// Initial Load
fetchAttendees();
fetchPrintJobs();
setInterval(fetchPrintJobs, 3000);
