import $ from "jquery";
import {createTomSelect} from "./selects";

const loadingHtml = "<div class='flex flex-col items-center justify-center py-8'><span class='loading loading-spinner loading-lg text-primary'></span><p class='mt-4 text-base-content/60'>Loading...</p></div>";

// Get dialog element and show it
function showDialog(dialogId) {
    const dialog = document.getElementById(dialogId);
    if (dialog && typeof dialog.showModal === 'function') {
        dialog.showModal();
    }
}

// Close dialog element
function closeDialog(dialogId) {
    const dialog = document.getElementById(dialogId);
    if (dialog && typeof dialog.close === 'function') {
        dialog.close();
    }
}

// Ajax form modal
export function showAjaxFormModal(url, title, fullScreen) {
    let $modal = $("#ajax-modal");
    let $modalBox = $modal.find(".modal-box");

    $modal.find(".title-text").html(title);
    $modal.find(".modal-body").html(loadingHtml);

    // Handle fullscreen mode
    if (fullScreen) {
        $modalBox.removeClass("w-11/12 max-w-5xl max-h-[90vh]").addClass("w-full h-full max-w-full max-h-full rounded-none");
    } else {
        $modalBox.removeClass("w-full h-full max-w-full max-h-full rounded-none").addClass("w-11/12 max-w-5xl max-h-[90vh]");
    }

    const prepareForms = function ($modal) {
        $modal.find("form").each(function () {
            const $this = $(this);
            $this.removeData("validator");
            $this.removeData("unobtrusiveValidation");
            $.validator.unobtrusive.parse($this);
        });
        createTomSelect($modal[0]);

        // When the modal form is submitted (exclude backdrop form)
        const $modalForms = $modal.find(".modal-body form");
        $modalForms.off("submit");
        $modalForms.on("submit", function (e) {
            e.preventDefault();
            const $this = $(this);
            const $form = $this.closest("form");

            // Validates the form
            if (!$form.valid()) {
                return false;
            }

            $.post($form.attr("action"), $form.serialize(), function (data) {
                if (typeof data === "object") {
                    if (data.success) {
                        if (data.redirect) {
                            window.location = data.redirect;
                        } else {
                            window.location.reload();
                        }
                    } else {
                        if (data.message) {
                            showMessage(data.message, "Danger");
                        } else {
                            window.location.reload();
                        }
                    }
                } else {
                    $modal.find(".modal-body").html(data);
                    prepareForms($modal);
                }
            }).catch(function (error) {
                let errorText = error.responseText;
                if (!errorText) {
                    errorText = "An error occurred, please try again later.";
                }
                $modal.find(".modal-body").html(`<div class="alert alert-error">${errorText}</div>`);
            });
            return false;
        });
    }

    // Loads the modal content on get
    $.get(url, function (data) {
        $modal.find(".modal-body").html(data);
        prepareForms($modal);
    }).catch(function (error) {
        let errorText = error.responseText;
        if (!errorText) {
            errorText = "An error occurred, please try again later.";
        }
        $modal.find(".modal-body").html(`<div class="alert alert-error">${errorText}</div>`);
    });

    // Shows the modal using native dialog API
    showDialog("ajax-modal");
}
window.showAjaxFormModal = showAjaxFormModal;

// Ajax show view in modal
export function showAjaxViewModal(url, title, fullScreen) {
    let $modal = $("#ajax-modal");
    let $modalBox = $modal.find(".modal-box");

    $modal.find(".title-text").html(title);
    $modal.find(".modal-body").html(loadingHtml);

    // Handle fullscreen mode
    if (fullScreen) {
        $modalBox.removeClass("w-11/12 max-w-5xl max-h-[90vh]").addClass("w-full h-full max-w-full max-h-full rounded-none");
    } else {
        $modalBox.removeClass("w-full h-full max-w-full max-h-full rounded-none").addClass("w-11/12 max-w-5xl max-h-[90vh]");
    }

    // Loads the modal content on get
    $.get(url, function (data) {
        $modal.find(".modal-body").html(data);
    }).catch(function (error) {
        let errorText = error.responseText;
        if (!errorText) {
            errorText = "An error occurred, please try again later.";
        }
        $modal.find(".modal-body").html(`<div class="alert alert-error">${errorText}</div>`);
    });

    // Shows the modal using native dialog API
    showDialog("ajax-modal");
}
window.showAjaxViewModal = showAjaxViewModal;

// Close modal function
export function closeAjaxModal() {
    closeDialog("ajax-modal");
}
window.closeAjaxModal = closeAjaxModal;

$(document).ready(function () {
    // Ajax form modal trigger
    $(document).on("click", ".show-ajax-form-modal", function (e) {
        e.preventDefault();
        const url = $(this).data("url");
        const title = $(this).data("title");
        const fullScreen = $(this).data("fullscreen");
        showAjaxFormModal(url, title, fullScreen);
    });

    // Ajax view modal trigger
    $(document).on("click", ".show-ajax-view-modal", function (e) {
        e.preventDefault();
        const url = $(this).data("url");
        const title = $(this).data("title");
        const fullScreen = $(this).data("fullscreen");
        showAjaxViewModal(url, title, fullScreen);
    });

    // Close button handler for ajax modal
    $(document).on("click", "#ajax-modal .modal-close", function (e) {
        e.preventDefault();
        closeDialog("ajax-modal");
    });

    // Prevent closing on backdrop click for ajax-modal (static backdrop behavior)
    const ajaxModal = document.getElementById("ajax-modal");
    if (ajaxModal) {
        ajaxModal.addEventListener("cancel", function (e) {
            // Allow ESC to close
        });
    }
});
