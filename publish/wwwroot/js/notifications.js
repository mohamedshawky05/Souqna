(() => {
    const widget = document.querySelector(".notification-widget");
    if (!widget) return;

    const endpoint = widget.dataset.notificationsUrl;
    const badge = widget.querySelector(".notification-badge");
    const list = widget.querySelector(".notification-list");
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    function setUnreadCount(count) {
        const safeCount = Math.max(0, Number(count) || 0);
        badge.textContent = safeCount > 9 ? "9+" : String(safeCount);
        badge.style.display = safeCount > 0 ? "" : "none";
    }

    function createNotificationElement(notification) {
        const container = document.createElement("div");
        container.className = `notification-item border-bottom p-3${notification.isRead ? "" : " bg-light"}`;
        container.dataset.notificationId = notification.id;
        container.dataset.actionUrl = notification.actionUrl ?? "";

        const title = document.createElement("div");
        title.className = "fw-bold small";
        title.textContent = notification.title;

        const message = document.createElement("div");
        message.className = "small text-muted mt-1";
        message.textContent = notification.message;

        const created = document.createElement("time");
        created.className = "d-block text-muted mt-2";
        created.style.fontSize = "0.75rem";
        created.dateTime = notification.createdAt;
        created.textContent = new Intl.DateTimeFormat("ar-EG", {
            dateStyle: "medium",
            timeStyle: "short"
        }).format(new Date(notification.createdAt));

        container.append(title, message, created);

        const actionUrl = notification.actionUrl;
        if (actionUrl) {
            const url = new URL(actionUrl, window.location.origin);
            if (url.origin === window.location.origin) {
                const link = document.createElement("a");
                link.className = "stretched-link";
                link.href = url.href;
                link.setAttribute("aria-label", notification.title);
                container.classList.add("position-relative");
                container.append(link);
            }
        }

        return container;
    }

    function renderNotifications(notifications) {
        list.replaceChildren();
        if (!notifications.length) {
            const empty = document.createElement("div");
            empty.className = "text-muted small text-center p-3";
            empty.textContent = "لا توجد إشعارات.";
            list.append(empty);
            return;
        }

        for (const notification of notifications) {
            list.append(createNotificationElement(notification));
        }
    }

    async function loadNotifications() {
        const response = await fetch(endpoint, { credentials: "same-origin" });
        if (!response.ok) throw new Error(`Notification load failed (${response.status})`);
        const data = await response.json();
        renderNotifications(data.notifications);
        setUnreadCount(data.unreadCount);
    }

    async function post(url) {
        const response = await fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: { RequestVerificationToken: token }
        });
        if (!response.ok) throw new Error(`Notification update failed (${response.status})`);
    }

    widget.addEventListener("show.bs.dropdown", () => {
        loadNotifications().catch(error => console.error(error));
    });

    widget.querySelector(".notification-mark-all").addEventListener("click", async event => {
        event.preventDefault();
        try {
            await post(`${endpoint}/read-all`);
            await loadNotifications();
        } catch (error) {
            console.error(error);
        }
    });

    list.addEventListener("click", async event => {
        const item = event.target.closest("[data-notification-id]");
        if (!item || !item.classList.contains("bg-light")) return;
        const link = event.target.closest("a[href]");
        if (link) event.preventDefault();

        try {
            await post(`${endpoint}/${item.dataset.notificationId}/read`);
            item.classList.remove("bg-light");
            await loadNotifications();
            if (link) window.location.assign(link.href);
        } catch (error) {
            console.error(error);
        }
    });

    loadNotifications().catch(error => console.error(error));

    if (!window.signalR) {
        console.error("SignalR client library is unavailable.");
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/notificationHub")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveNotification", notification => {
        const current = [...list.querySelectorAll("[data-notification-id]")]
            .map(item => ({
                id: Number(item.dataset.notificationId),
                title: item.querySelector(".fw-bold")?.textContent ?? "",
                message: item.querySelector(".text-muted")?.textContent ?? "",
                actionUrl: item.dataset.actionUrl || null,
                isRead: !item.classList.contains("bg-light"),
                createdAt: item.querySelector("time")?.dateTime ?? new Date().toISOString()
            }));
        renderNotifications([
            notification,
            ...current.filter(item => item.id !== notification.id)
        ].slice(0, 10));
    });

    connection.on("UnreadCountChanged", setUnreadCount);
    connection.start().catch(error => console.error("Notification connection failed.", error));
})();
