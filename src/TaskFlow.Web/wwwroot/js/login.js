(() => {
    const form = document.querySelector("[data-loading-form]");
    const button = form?.querySelector("[data-loading-button]");
    const label = button?.querySelector("[data-button-label]");
    const spinner = button?.querySelector(".loading-spinner");
    if (!form || !button || !label || !spinner) return;

    form.addEventListener("submit", () => {
        button.disabled = true;
        button.setAttribute("aria-busy", "true");
        label.textContent = "Entrando...";
        spinner.hidden = false;
    });
})();
