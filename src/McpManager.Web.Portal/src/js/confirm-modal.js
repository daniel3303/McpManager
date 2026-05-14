import $ from 'jquery';

function Confirm(text, functionYes, functionNo, textConfirm, textCancel) {
    if (text === undefined || text === null || text === "")
        text = "Deseja continuar?";

    const dialog = document.getElementById("confirm-form-modal");
    const $modal = $("#confirm-form-modal");

    // Set the confirmation text
    $modal.find(".text").text(text);

    // Set custom button text if provided
    if (textConfirm) {
        $modal.find(".text-confirm").text(textConfirm);
    } else {
        $modal.find(".text-confirm").text("Yes");
    }

    if (textCancel) {
        $modal.find(".text-cancel").text(textCancel);
    } else {
        $modal.find(".text-cancel").text("No");
    }

    // Show the dialog using native API
    if (dialog && typeof dialog.showModal === 'function') {
        dialog.showModal();
    }

    // Handle keyboard events
    $(document).off("keyup.confirm").on("keyup.confirm", function (e) {
        let code = e.which;
        if (code === 13 || code === 32) {
            $modal.find(".confirm").trigger("click");
            $(document).off("keyup.confirm");
        } else if (code === 27) {
            $modal.find(".cancel").trigger("click");
            $(document).off("keyup.confirm");
        }
    });

    // Handle confirm button click
    $modal.find(".confirm")
        .off("click")
        .on("click", function () {
            if (typeof functionYes === 'function') {
                functionYes();
            }
            if (dialog && typeof dialog.close === 'function') {
                dialog.close();
            }
        })
        .trigger("focus");

    // Handle cancel button click
    $modal.find(".cancel")
        .off("click")
        .on("click", function () {
            if (typeof functionNo === 'function') {
                functionNo();
            }
            if (dialog && typeof dialog.close === 'function') {
                dialog.close();
            }
        });

    // Prevent closing on ESC (handled by our keyup handler)
    if (dialog) {
        dialog.addEventListener("cancel", function (e) {
            e.preventDefault();
            $modal.find(".cancel").trigger("click");
        }, { once: true });
    }
}

$(document).ready(function () {
    // Form confirmation handler
    $("body").on("submit", ".form-confirm", function (event) {
        if ($(this).hasClass("form-confirm")) {
            event.preventDefault();
            event.stopPropagation();
            let $element = $(this);

            Confirm($element.data("message"), function () {
                $element.removeClass("form-confirm");
                $element.submit();
            });
            return false;
        }
    });

    // Button confirmation handler
    $("body").on("click", ".btn-confirm", function (event) {
        if ($(this).hasClass("btn-confirm")) {
            event.preventDefault();
            event.stopPropagation();
            const $element = $(this);

            Confirm($element.data("message"), function () {
                $element.removeClass("btn-confirm");
                var href = $element.attr('href') !== undefined && $element.attr('href') !== "" ? $element.attr('href') : "";
                if (href !== "") {
                    window.location.href = href;
                } else {
                    $element.trigger("click");
                }
            });
            return false;
        }
    });
});

window.Confirm = Confirm;
