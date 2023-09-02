﻿// Helper Function for Select2
function formatState(state) {
    if (!state.id) {
        return state.text;
    }

    if (state.element.dataset.content == null) {
        return state.text;
    }

    const span = document.createElement("span");
    span.innerHTML = state.element.dataset.content;

    return span;
}

function errorLog(x) {
    console.log("An Error has occurred!");
    console.log(x.responseText);
    console.log(x.status);
}

function wrap(el, wrapper) {
    el.parentNode.insertBefore(wrapper, el);
    wrapper.appendChild(el);
}

function empty(wrap) {
    while (wrap.firstChild) wrap.removeChild(wrap.firstChild);
}

// Attachments/Albums Popover Preview
function renderAttachPreview(previewClass) {
    document.querySelectorAll(previewClass).forEach(attach => {
        return new bootstrap.Popover(attach,
            {
                html: true,
                trigger: "hover",
                placement: "bottom",
                content: function () { return `<img src="${attach.src}" class="img-fluid" />`; }
            });
    });
}

// Confirm Dialog
document.addEventListener("click", function (event) {
    if (event.target.parentElement.matches('[data-bs-toggle="confirm"]')) {
        event.preventDefault();
        var button = event.target.parentElement;

        const text = button.dataset.title,
            yes = button.dataset.yes,
            no = button.dataset.no,
            title = button.innerHTML;

        button.innerHTML = "<span class='spinner-border spinner-border-sm' role='status' aria-hidden='true'></span> Loading...";

        bootboxConfirm(button, title, text, yes, no, function (r) {
            if (r) {
                button.click();
            }
        });
    }
}, false);

var bootboxConfirm = function (button, title, message, yes, no, callback) {
    const options = {
        message: message,
        centerVertical: true,
        title: title
    };
    options.buttons = {
        cancel: {
            label: `<i class="fa fa-times"></i> ${no}`,
            className: "btn-danger",
            callback: function (result) {
                callback(false);
                button.innerHTML = title;
            }
        },
        main: {
            label: `<i class="fa fa-check"></i> ${yes}`,
            className: "btn-success",
            callback: function (result) {
                callback(true);
            }
        }
    };
    bootbox.dialog(options);
};

document.addEventListener("DOMContentLoaded", function () {

    if (document.querySelector(".btn-scroll") != null) {
        // Scroll top button
        var scrollToTopBtn = document.querySelector(".btn-scroll"), rootElement = document.documentElement;

        function handleScroll() {
            const scrollTotal = rootElement.scrollHeight - rootElement.clientHeight;
            if ((rootElement.scrollTop / scrollTotal) > 0.15) {
                // Show button
                scrollToTopBtn.classList.add("show-btn-scroll");
            } else {
                // Hide button
                scrollToTopBtn.classList.remove("show-btn-scroll");
            }
        }

        function scrollToTop(e) {
            e.preventDefault();

            // Scroll to top logic
            rootElement.scrollTo({
                top: 0,
                behavior: "smooth"
            });
        }

        scrollToTopBtn.addEventListener("click", scrollToTop);
        document.addEventListener("scroll", handleScroll);
    }

    // Toggle password visibility
    if (document.body.contains(document.getElementById("PasswordToggle"))) {
        const passwordToggle = document.getElementById("PasswordToggle");
        var icon = passwordToggle.querySelector("i"),
        pass = document.querySelector("input[id*='Password']");
        passwordToggle.addEventListener("click", function (event) {
            event.preventDefault();
            if (pass.getAttribute("type") === "text") {
                pass.setAttribute("type", "password");
                icon.classList.add("fa-eye-slash");
                icon.classList.remove("fa-eye");
            } else if (pass.getAttribute("type") === "password") {
                pass.setAttribute("type", "text");
                icon.classList.remove("fa-eye-slash");
                icon.classList.add("fa-eye");
            }
        });
    }
})