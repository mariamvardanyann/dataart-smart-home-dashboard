const list = document.getElementById('device-list');
const message = document.getElementById('message');
const refreshButton = document.getElementById('refresh-button');
let isRefreshing = false;

function addText(parent, tag, className, value) {
  const element = document.createElement(tag);
  if (className) element.className = className;
  element.textContent = value ?? '—';
  parent.appendChild(element);
  return element;
}

function renderDevice(device) {
  const card = document.createElement('article');
  card.className = 'device';
  const top = addText(card, 'div', 'device-top', '');
  addText(top, 'h3', 'device-title', device.name);
  addText(top, 'span', 'status ' + (device.isOnline ? 'online' : 'offline'),
    device.isOnline ? 'Online' : 'Offline');
  addText(card, 'span', 'endpoint', device.infoUrl);

  if (device.isOnline) {
    const metrics = addText(card, 'dl', 'metrics', '');
    const entries = Object.entries(device.metrics ?? {});
    if (!entries.length) addText(card, 'p', 'offline-message', 'No metrics reported.');
    for (const [key, value] of entries) {
      const metric = addText(metrics, 'div', 'metric', '');
      addText(metric, 'dt', '', key);
      addText(metric, 'dd', '', String(value));
    }
    addText(card, 'div', 'device-bottom', 'Network address: ' + (device.address ?? 'Unknown'));
  } else {
    addText(card, 'p', 'offline-message', device.error ?? 'Device unavailable.');
    addText(card, 'div', 'device-bottom', 'Start the emulator to reconnect.');
  }
  return card;
}

async function refresh() {
  if (isRefreshing) return;
  isRefreshing = true;
  refreshButton.disabled = true;
  try {
    const response = await fetch('/api/dashboard', { cache: 'no-store' });
    if (!response.ok) throw new Error('HTTP ' + response.status);
    const snapshot = await response.json();
    document.getElementById('total-devices').textContent = snapshot.totalDevices;
    document.getElementById('online-devices').textContent = snapshot.onlineDevices;
    document.getElementById('offline-devices').textContent = snapshot.offlineDevices;
    document.getElementById('last-updated').textContent =
      'Last checked: ' + new Date(snapshot.checkedAt).toLocaleTimeString();

    list.replaceChildren();
    if (!snapshot.devices.length) addText(list, 'p', 'empty', 'No devices configured.');
    else for (const device of snapshot.devices) list.appendChild(renderDevice(device));
    message.hidden = true;
  } catch (error) {
    message.textContent = 'Could not load status: ' + error.message;
    message.hidden = false;
  } finally {
    isRefreshing = false;
    refreshButton.disabled = false;
  }
}

refreshButton.addEventListener('click', refresh);
refresh();
setInterval(refresh, 5000);
