const els = {
  userId: document.getElementById("userId"),
  price: document.getElementById("price"),
  btnConnect: document.getElementById("btnConnect"),
  btnCreateOrder: document.getElementById("btnCreateOrder"),
  btnRefresh: document.getElementById("btnRefresh"),
  btnAskNotify: document.getElementById("btnAskNotify"),
  orders: document.getElementById("orders"),
  toasts: document.getElementById("toasts"),
};

let connection = null;

function toast(text) {
  const el = document.createElement("div");
  el.className = "toast";
  el.textContent = text;
  els.toasts.appendChild(el);
  setTimeout(() => el.remove(), 4500);
}

async function askNotifications() {
  if (!("Notification" in window)) {
    toast("Браузер не поддерживает уведомления");
    return;
  }
  const res = await Notification.requestPermission();
  toast("Notification permission: " + res);
}

function pushNotify(title, body) {
  if (!("Notification" in window)) return;
  if (Notification.permission !== "granted") return;
  new Notification(title, { body });
}

async function connectWs() {
  const userId = els.userId.value.trim();
  if (!userId) return toast("Введите userId");

  if (connection) {
    await connection.stop();
    connection = null;
  }

  const url = `/orders/ws/orders?userId=${encodeURIComponent(userId)}`;

  connection = new signalR.HubConnectionBuilder()
    .withUrl(url)
    .withAutomaticReconnect()
    .build();

  connection.on("OrderStatusChanged", (evt) => {
    const msg = `Заказ ${evt.orderId}: ${evt.status}`;
    toast(msg);
    pushNotify("GoZon", msg);
    refreshOrders();
  });

  await connection.start();
  toast("WS подключен для userId=" + userId);

  await refreshOrders();
}

async function createOrder() {
  const userId = els.userId.value.trim();
  const price = Number(els.price.value);

  if (!userId) return toast("Введите userId");
  if (!Number.isFinite(price) || price <= 0) return toast("Некорректная цена");

  const res = await fetch("/orders/api/orders", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ userId, price }),
  });

  const body = await res.json().catch(() => ({}));
  if (!res.ok) return toast("Ошибка: " + JSON.stringify(body));

  toast(`Создан заказ ${body.orderId}, статус: ${body.status}`);
  await refreshOrders();
}

function renderOrders(list) {
  els.orders.innerHTML = "";
  for (const o of list) {
    const div = document.createElement("div");
    div.className = "order";
    div.innerHTML = `
      <div>
        <div><b>${o.id}</b></div>
        <div class="muted">price=${o.price} created=${new Date(o.createdAt).toLocaleString()}</div>
      </div>
      <div class="badge">${o.status}</div>
    `;
    els.orders.appendChild(div);
  }
}

async function refreshOrders() {
  const userId = els.userId.value.trim();
  if (!userId) return;

  const res = await fetch(`/orders/api/orders?userId=${encodeURIComponent(userId)}`);
  const list = await res.json().catch(() => []);
  if (!res.ok) return toast("Ошибка списка заказов");

  renderOrders(list);
}

els.btnConnect.addEventListener("click", connectWs);
els.btnCreateOrder.addEventListener("click", createOrder);
els.btnRefresh.addEventListener("click", refreshOrders);
els.btnAskNotify.addEventListener("click", askNotifications);
