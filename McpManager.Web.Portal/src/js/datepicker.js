
import flatpickr from "flatpickr";
import monthSelectPlugin from "flatpickr/dist/plugins/monthSelect";

const selector = "input[type=date], input[type=datetime-local]";

export function createFlatPicker(elements){
    const dateOptions = {
        altFormat: 'd-m-Y',
        altInput: true,
        allowInput: true,
    };

    const timeOptions = {
        enableTime: true,
        noCalendar: true,
        dateFormat: "H:i",
        time_24hr: true,
        allowInput: true,
    }

    const dateAndTimeOptions = {
        altFormat: 'd-m-Y H:i',
        altInput: true,
        allowInput: true,
        enableTime: true,
        time_24hr: true
    }

    for(let element of elements){
        const type = element.getAttribute("type");
        if(type === "datetime-local"){
            new flatpickr(element, dateAndTimeOptions);
        }else if(type === "time"){
            new flatpickr(element, timeOptions);
        }else{
            new flatpickr(element, dateOptions);
        }

    }
}

createFlatPicker(document.querySelectorAll(selector));


// Mutation observer to apply flat pickr to new elements
const observer = new MutationObserver(function (mutations, observer) {
    // Look through all mutations that just occured
    for (let i = 0; i < mutations.length; ++i) {
        // Look through all added nodes of this mutation
        for (let j = 0; j < mutations[i].addedNodes.length; ++j) {
            // Was a child added matching the selector?
            const element = mutations[i].addedNodes[j];
            if (element.matches && element.matches(selector)) {
                createFlatPicker(mutations[i].addedNodes[j])
            }else if(element.querySelectorAll){
                createFlatPicker(element.querySelectorAll(selector))
            }
        }
    }
});

// Have the observer observe the document for changes in children
observer.observe(document, {childList: true, subtree: true });
