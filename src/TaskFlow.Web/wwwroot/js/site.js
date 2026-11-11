(() => {
    const sidebar = document.querySelector(".sidebar");
    const toggle = document.querySelector("[data-nav-toggle]");
    const close = document.querySelector("[data-nav-close]");
    if (!sidebar || !toggle || !close) return;

    const setOpen = (open) => {
        sidebar.classList.toggle("is-open", open);
        toggle.setAttribute("aria-expanded", String(open));
        toggle.setAttribute("aria-label", open ? "Fechar menu" : "Abrir menu");
        close.hidden = !open;
    };

    toggle.addEventListener("click", () => setOpen(!sidebar.classList.contains("is-open")));
    close.addEventListener("click", () => setOpen(false));
    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") setOpen(false);
    });
})();
