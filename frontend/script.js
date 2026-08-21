const API_BASE = '/api';

let attendees = [];
let printJobs = [];
let selectedAttendeeId = 'A001';
let currentViewState = 'DEFAULT'; // 'DEFAULT', 'PENDING', 'SUCCESS', 'DUPLICATE'

document.addEventListener('DOMContentLoaded', () => {
    fetchAttendees();
    fetchPrintJobs();
    setInterval(() => {
        fetchAttendees(true);
        fetchPrintJobs(true);
    }, 1200);
});

function showToast(message, icon = 'ℹ️', duration = 4000) {
    const toast = document.getElementById('toast');
    const msg = document.getElementById('toastMsg');
    const ic = document.getElementById('toastIcon');

    msg.textContent = message;
    ic.textContent = icon;
    toast.classList.remove('hidden');

    setTimeout(() => {
        toast.classList.add('hidden');
    }, duration);
}

// Fetch Attendees
async function fetchAttendees(silent = false) {
    try {
        const res = await fetch(`${API_BASE}/attendees`);
        if (!res.ok) return;
        attendees = await res.json();
        renderSideAttendees();
        updateCenterCard();
    } catch (err) {
        if (!silent) console.error(err);
    }
}

// Fetch Print Jobs
async function fetchPrintJobs(silent = false) {
    try {
        const res = await fetch(`${API_BASE}/print-jobs`);
        if (!res.ok) return;
        printJobs = await res.json();
        renderAuditTable();
    } catch (err) {
        if (!silent) console.error(err);
    }
}

// Select Attendee from Side List
function selectAttendee(id) {
    selectedAttendeeId = id;
    currentViewState = 'DEFAULT';
    renderSideAttendees();
    updateCenterCard();
}

// Render Side Attendee List
function renderSideAttendees() {
    const list = document.getElementById('attendeesList');
    if (!list) return;

    list.innerHTML = attendees.map(a => {
        const isActive = a.id === selectedAttendeeId ? 'active' : '';
        const initials = a.name.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();

        let badgeClass = 'not_checked_in';
        let badgeLabel = 'Ready';

        if (a.status === 'PENDING') {
            badgeClass = 'pending';
            badgeLabel = '⏳ Printing';
        } else if (a.status === 'CHECKED_IN') {
            badgeClass = 'checked_in';
            badgeLabel = '✓ Checked In';
        }

        return `
            <div class="attendee-row ${isActive}" onclick="selectAttendee('${a.id}')">
                <div class="row-left">
                    <div class="avatar-sm">${initials}</div>
                    <div>
                        <div class="row-name">${a.name}</div>
                        <div class="row-id">${a.id}</div>
                    </div>
                </div>
                <span class="badge-status ${badgeClass}">${badgeLabel}</span>
            </div>
        `;
    }).join('');
}

// Update Central Stage Card
function updateCenterCard() {
    const attendee = attendees.find(a => a.id === selectedAttendeeId) || { id: 'A001', name: 'Alice', status: 'NOT_CHECKED_IN' };

    const pill = document.getElementById('cardTopPill');
    const pillText = document.getElementById('pillText');
    const icon = document.getElementById('modalIcon');
    const iconSym = document.getElementById('iconSymbol');
    const subheading = document.getElementById('modalSubheading');
    const name = document.getElementById('modalName');
    const idElem = document.getElementById('modalId');
    const desc = document.getElementById('modalDesc');
    const btn = document.getElementById('modalBtn');
    const footer = document.getElementById('modalFooterNote');

    name.textContent = attendee.name;
    idElem.textContent = attendee.id;

    // Reset theme classes
    pill.className = 'status-top-pill';
    icon.className = 'modal-icon-circle';
    subheading.className = 'modal-subheading';
    btn.className = 'modal-pill-btn';
    btn.disabled = false;

    if (currentViewState === 'DUPLICATE') {
        // EXACT MATCH TO UPLOADED SCREENSHOT
        pill.classList.add('amber');
        pillText.textContent = 'ALREADY CHECKED IN';
        
        icon.classList.add('amber');
        iconSym.textContent = '!';

        subheading.classList.add('amber');
        subheading.textContent = 'ALREADY CHECKED IN';

        desc.textContent = 'This attendee has already received a badge.';
        
        btn.classList.add('amber');
        btn.textContent = 'OK';
        btn.onclick = () => { currentViewState = 'DEFAULT'; updateCenterCard(); };

        footer.textContent = 'NO NEW BADGE WAS PRINTED';
    } else if (attendee.status === 'PENDING') {
        // PENDING STATE (PRINTING VIA RABBITMQ)
        pill.classList.add('amber');
        pillText.textContent = 'PRINTING IN PROGRESS';

        icon.classList.add('amber');
        iconSym.textContent = '⏳';

        subheading.classList.add('amber');
        subheading.textContent = 'PRINTING BADGE...';

        desc.textContent = 'Badge print request queued in RabbitMQ. Simulating hardware print...';

        btn.classList.add('amber');
        btn.textContent = 'PROCESSING...';
        btn.disabled = true;

        footer.textContent = 'PUBLISHED TO QUEUE: BADGE-PRINT-REQUESTS';
    } else if (attendee.status === 'CHECKED_IN') {
        // COMPLETED STATE
        pill.classList.add('emerald');
        pillText.textContent = 'BADGE ISSUED';

        icon.classList.add('emerald');
        iconSym.textContent = '✓';

        subheading.classList.add('emerald');
        subheading.textContent = 'CHECKED IN';

        desc.textContent = 'Badge printed and attendee confirmed in database.';

        btn.classList.add('emerald');
        btn.textContent = 'RE-SCAN BADGE';
        btn.onclick = () => handleCenterAction();

        footer.textContent = '1 BADGE PRINTED VIA RABBITMQ';
    } else {
        // NOT_CHECKED_IN (READY TO SCAN)
        pill.classList.add('cyan');
        pillText.textContent = 'READY TO SCAN';

        icon.classList.add('cyan');
        iconSym.textContent = '📷';

        subheading.classList.add('cyan');
        subheading.textContent = 'READY TO CHECK IN';

        desc.textContent = 'Press "Scan Badge" to initiate asynchronous printing via RabbitMQ.';

        btn.classList.add('cyan');
        btn.textContent = 'SCAN BADGE';
        btn.onclick = () => handleCenterAction();

        footer.textContent = 'RABBITMQ ASYNCHRONOUS CHECK-IN SYSTEM';
    }
}

// Center Button Action
async function handleCenterAction() {
    const attendee = attendees.find(a => a.id === selectedAttendeeId);
    if (!attendee) return;

    if (attendee.status === 'CHECKED_IN' || attendee.status === 'PENDING') {
        // Duplicate scan attempted -> trigger warning view
        currentViewState = 'DUPLICATE';
        updateCenterCard();
        showToast(`Duplicate scan prevented for ${attendee.name}`, '🛡️');
        return;
    }

    // Trigger Check-in API
    try {
        const res = await fetch(`${API_BASE}/checkin/${attendee.id}`, { method: 'POST' });
        const data = await res.json();

        if (res.ok && data.status === 'PENDING') {
            showToast(`Printing badge for ${attendee.name}...`, '⚡');
            fetchAttendees();
            fetchPrintJobs();
        } else if (data.status === 'DUPLICATE') {
            currentViewState = 'DUPLICATE';
            updateCenterCard();
        } else if (data.status === 'BROKER_UNREACHABLE') {
            showToast(data.message, '⚠️', 6000);
        } else {
            showToast(data.message || 'Scan error occurred.', '⚠️');
        }
    } catch (err) {
        console.error(err);
        showToast('Connection Error: Backend server unreachable.', '❌');
    }
}

// Trigger Duplicate Demo Button (Side Panel)
function triggerDuplicateDemo() {
    const checkedInAttendee = attendees.find(a => a.status === 'CHECKED_IN') || attendees[2] || attendees[0];
    selectedAttendeeId = checkedInAttendee.id;
    currentViewState = 'DUPLICATE';
    renderSideAttendees();
    updateCenterCard();
    showToast(`Duplicate scan simulation for ${checkedInAttendee.name}`, '🛡️');
}

// Trigger Duplicate Webhook Test
async function testDuplicateWebhook() {
    const payload = {
        eventId: 'evt-demo-duplicate-99',
        jobId: 'job-test-demo',
        attendeeId: 'A001',
        status: 'COMPLETED',
        completedAt: new Date().toISOString()
    };

    try {
        const res1 = await fetch(`${API_BASE}/webhooks/print-completed`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        const d1 = await res1.json();

        const res2 = await fetch(`${API_BASE}/webhooks/print-completed`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        const d2 = await res2.json();

        showToast(`Idempotency OK: [${d1.status}] & [${d2.status}]`, '⚡', 4000);
        fetchAttendees();
        fetchPrintJobs();
    } catch (err) {
        console.error(err);
    }
}

// Reset System State
async function resetSystem() {
    try {
        const res = await fetch(`${API_BASE}/reset`, { method: 'POST' });
        if (res.ok) {
            currentViewState = 'DEFAULT';
            showToast('Kiosk State Reset', '🔄');
            fetchAttendees();
            fetchPrintJobs();
        }
    } catch (err) {
        console.error(err);
    }
}

// Render Audit Log Table
function renderAuditTable() {
    const tbody = document.getElementById('auditTableBody');
    const count = document.getElementById('auditCount');
    if (!tbody) return;

    if (count) count.textContent = `${printJobs.length} Jobs`;

    if (printJobs.length === 0) {
        tbody.innerHTML = `<tr><td colspan="4" class="empty-log">No jobs in queue yet.</td></tr>`;
        return;
    }

    tbody.innerHTML = printJobs.slice(-5).reverse().map(j => `
        <tr>
            <td><code style="color: var(--amber-primary); font-family: var(--font-mono);">${j.id.substring(0, 8)}</code></td>
            <td>${j.attendeeId}</td>
            <td><span style="color: ${j.status === 'COMPLETED' ? '#10b981' : '#f59e0b'}; font-weight: 700;">${j.status}</span></td>
            <td>${new Date(j.createdAt).toLocaleTimeString()}</td>
        </tr>
    `).join('');
}
