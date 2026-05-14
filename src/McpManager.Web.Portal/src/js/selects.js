import $ from 'jquery';
import TomSelect from 'tom-select';
import 'tom-select/dist/css/tom-select.css';

// Initialize Tom Select on document ready
$(document).ready(function() {
    createTomSelect();
});

/**
 * Remove accents from string for accent-insensitive search
 */
export function removeAccents(str) {
    const accents = "ÀÁÂÃÄÅàáâãäåÒÓÔÕÕÖØòóôõöøÈÉÊËèéêëðÇçÐÌÍÎÏìíîïÙÚÛÜùúûüÑñŠšŸÿýŽž";
    const accentsOut = "AAAAAAaaaaaaOOOOOOOooooooEEEEeeeeeCcDIIIIiiiiUUUUuuuuNnSsYyyZz";
    return str.split("").map(char => {
        const index = accents.indexOf(char);
        return index !== -1 ? accentsOut[index] : char;
    }).join("");
}

/**
 * Custom score function for accent-insensitive multi-word matching
 */
function accentInsensitiveScore(search) {
    const searchTerms = search.toLowerCase().split(" ")
        .map(term => removeAccents(term.trim()))
        .filter(term => term !== "");

    return function(option) {
        const text = removeAccents((option.text || "").toLowerCase());

        // All search terms must match
        for (const term of searchTerms) {
            if (text.indexOf(term) < 0) {
                return 0;
            }
        }
        return 1;
    };
}

/**
 * Initialize Tom Select on elements with .tom-select-element class
 * @param {HTMLElement|jQuery} container - Optional container to scope initialization
 */
export function createTomSelect(container) {
    const scope = container instanceof $
        ? container[0]
        : (container || document);

    const selects = scope.querySelectorAll('select:not(.tomselected):not(.flatpickr-monthDropdown-months)');

    selects.forEach(select => {
        const isMultiple = select.multiple;
        const allowSearch = select.dataset.search !== 'false';
        const allowClear = select.dataset.clear !== 'false';
        const placeholder = select.dataset.placeholder || '';

        // Build plugins object
        const plugins = {};

        if (allowClear && !isMultiple) {
            plugins.clear_button = { title: 'Limpar' };
        }

        if (isMultiple) {
            plugins.checkbox_options = { checkedClassNames: ['ts-checked'], uncheckedClassNames: ['ts-unchecked'] };
            plugins.remove_button = { title: 'Remover' };
            plugins.no_backspace_delete = {};
        }

        if (allowSearch) {
            plugins.dropdown_input = {};
        }

        const config = {
            plugins: plugins,
            placeholder: placeholder,
            allowEmptyOption: true,
            score: accentInsensitiveScore,
            maxOptions: null,

            // Render functions
            render: {
                no_results: function(data, escape) {
                    return `<div class="ts-no-results">Sem resultados para "${escape(data.input)}"</div>`;
                }
            },

            // Event handlers
            onInitialize: function() {
                // Remove the 'select' class that Tom Select copies from the original element
                // to prevent double-border styling
                this.wrapper.classList.remove('select');
            },
            onChange: function(value) {
                // Trigger ASP.NET validation
                const form = select.closest('form');
                if (form && $.fn.validate) {
                    $(form).validate().element(select);
                }

                // Auto-submit if has submit-trigger class
                if (select.classList.contains('submit-trigger')) {
                    select.closest('form')?.submit();
                }
            }
        };

        // Disable search if data-search="false"
        if (!allowSearch) {
            config.controlInput = null;
        }

        // Store instance on element for later access
        select.tomSelect = new TomSelect(select, config);
    });
}

/**
 * Destroy Tom Select instance
 */
export function destroyTomSelect(select) {
    if (select.tomSelect) {
        select.tomSelect.destroy();
        select.tomSelect = null;
    }
}

/**
 * Update child select options based on parent selection (cascading selects)
 */
const defaultUpdateSelectOptions = {
    parentParamName: null,
};

export function updateSelectBasedOnParent(parentSelect, childSelect, endpoint, options = {}) {
    options = { ...defaultUpdateSelectOptions, ...options };

    // Handle jQuery objects
    const $parent = parentSelect instanceof $ ? parentSelect : $(parentSelect);
    const $child = childSelect instanceof $ ? childSelect : $(childSelect);
    const childEl = $child[0];

    const updateChildSelect = function(triggerChange = true) {
        const parentSelectedVal = $parent.val();
        const parentName = options.parentParamName ?? $parent.attr("name");
        const currentVal = $child.val();

        if (!parentSelectedVal) {
            $child.closest(".col-wrapper").addClass("hidden");

            // Clear Tom Select options
            if (childEl.tomSelect) {
                childEl.tomSelect.clear();
                childEl.tomSelect.clearOptions();
            } else {
                $child.find('option').remove();
            }

            if (triggerChange) $child.trigger("change");
            return;
        }

        $child.closest(".col-wrapper").removeClass("hidden");

        $.get(endpoint, { [parentName]: parentSelectedVal }, function(data) {
            if (childEl.tomSelect) {
                // Update Tom Select
                childEl.tomSelect.clear();
                childEl.tomSelect.clearOptions();
                childEl.tomSelect.addOption({ value: '', text: '' });

                for (const el of data) {
                    childEl.tomSelect.addOption({
                        value: el.id.toString(),
                        text: el.name
                    });
                }

                // Restore previous value if exists in new options
                if (currentVal) {
                    const hasOption = data.some(el => el.id?.toString() === currentVal?.toString());
                    if (hasOption) {
                        childEl.tomSelect.setValue(currentVal, !triggerChange);
                    }
                }

                childEl.tomSelect.refreshOptions(false);
            } else {
                // Fallback for plain selects
                $child.find('option').remove();
                $child.append(new Option("", "", true, true));
                for (const el of data) {
                    const option = new Option(el.name, el.id, false, currentVal?.toString() === el.id?.toString());
                    $child.append(option);
                }
                if (triggerChange) $child.trigger("change");
            }
        });
    };

    $parent.on("change", updateChildSelect);
    updateChildSelect(false);
}

window.updateSelectBasedOnParent = updateSelectBasedOnParent;
window.createTomSelect = createTomSelect;
