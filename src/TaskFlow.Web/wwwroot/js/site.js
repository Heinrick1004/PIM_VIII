(() => {
    const sidebar = document.querySelector(".sidebar");
    const toggle = document.querySelector("[data-nav-toggle]");
    const close = document.querySelector("[data-nav-close]");

    if (sidebar && toggle && close) {
        const mobileNav = window.matchMedia("(max-width: 820px)");
        const setOpen = (open, restoreFocus = false) => {
            sidebar.classList.toggle("is-open", open);
            sidebar.inert = mobileNav.matches && !open;
            toggle.setAttribute("aria-expanded", String(open));
            toggle.setAttribute("aria-label", open ? "Fechar menu" : "Abrir menu");
            close.hidden = !open;

            if (open) sidebar.querySelector("a, button")?.focus();
            else if (restoreFocus) toggle.focus();
        };

        setOpen(false);
        toggle.addEventListener("click", () => setOpen(!sidebar.classList.contains("is-open"), true));
        close.addEventListener("click", () => setOpen(false, true));
        mobileNav.addEventListener("change", () => setOpen(false));
        document.addEventListener("keydown", (event) => {
            if (event.key === "Escape" && sidebar.classList.contains("is-open")) setOpen(false, true);
        });
    }

    document.querySelectorAll("form[data-loading-form]").forEach((form) => {
        if (form.closest(".auth-page")) return;

        form.addEventListener("submit", () => {
            const button = form.querySelector("[data-loading-button]");
            const label = button?.querySelector("[data-button-label]");
            const spinner = button?.querySelector(".loading-spinner");
            if (!button || !label || !spinner || button.disabled) return;

            button.disabled = true;
            button.setAttribute("aria-busy", "true");
            label.textContent = (form.getAttribute("method") || "get").toLowerCase() === "get"
                ? "Filtrando..."
                : "Salvando...";
            spinner.hidden = false;
        });
    });
})();
