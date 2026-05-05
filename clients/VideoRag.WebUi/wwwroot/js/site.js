document.addEventListener("DOMContentLoaded", function () {
    const form = document.getElementById("ask-form");
    const button = document.getElementById("submit-button");
    const loader = button?.querySelector(".btn-loader");
    const buttonText = button?.querySelector(".btn-text");
    const textarea = document.getElementById("question-box");

    if (textarea) {
        const resize = () => {
            textarea.style.height = "auto";
            textarea.style.height = Math.min(textarea.scrollHeight, 320) + "px";
        };

        resize();
        textarea.addEventListener("input", resize);
    }

    if (form && button && loader && buttonText) {
        form.addEventListener("submit", function () {
            button.disabled = true;
            loader.classList.remove("hidden");
            buttonText.textContent = "Отправка...";
        });
    }
});