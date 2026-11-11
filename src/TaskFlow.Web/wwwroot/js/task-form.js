(() => {
    const input = document.querySelector("[data-due-local]");
    const offsetInput = document.querySelector("[data-timezone-offset]");
    if (!input || !offsetInput) return;

    const pad = (value) => String(value).padStart(2, "0");
    const localInputValue = (date) => [
        date.getFullYear(),
        "-",
        pad(date.getMonth() + 1),
        "-",
        pad(date.getDate()),
        "T",
        pad(date.getHours()),
        ":",
        pad(date.getMinutes()),
        ":",
        pad(date.getSeconds())
    ].join("");

    const selectedOffset = () => {
        const selectedDate = input.value ? new Date(input.value) : new Date();
        return selectedDate.getTimezoneOffset();
    };

    if (input.dataset.utcValue) {
        const utcDate = new Date(input.dataset.utcValue);
        if (!Number.isNaN(utcDate.getTime())) input.value = localInputValue(utcDate);
    }
    offsetInput.value = String(selectedOffset());

    const form = input.closest("form");
    form?.addEventListener("submit", () => {
        offsetInput.value = String(selectedOffset());
    });
})();
