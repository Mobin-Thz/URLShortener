document.addEventListener("click", async (event) => {
    const button = event.target.closest("[data-copy]");
    if (!button) {
        return;
    }

    const value = button.dataset.copy;
    if (!value) {
        return;
    }

    const originalText = button.textContent;
    try {
        await navigator.clipboard.writeText(value);
        button.textContent = "Copied";
    } catch {
        button.textContent = "Copy failed";
    }

    window.setTimeout(() => {
        button.textContent = originalText;
    }, 1400);
});

document.addEventListener("submit", (event) => {
    const form = event.target.closest("[data-processing-form]");
    if (!form) {
        return;
    }

    if (form.dataset.submitting === "true") {
        event.preventDefault();
        return;
    }

    event.preventDefault();
    form.dataset.submitting = "true";
    form.setAttribute("aria-busy", "true");

    form.querySelectorAll("[data-submit-button]").forEach((button) => {
        button.disabled = true;
        button.querySelector("[data-submit-idle]")?.setAttribute("hidden", "");
        button.querySelector("[data-submit-busy]")?.removeAttribute("hidden");
    });

    form.querySelector("[data-processing-hint]")?.removeAttribute("hidden");

    // Give the browser time to paint the busy state before navigation starts.
    window.setTimeout(() => {
        HTMLFormElement.prototype.submit.call(form);
    }, 120);
});
