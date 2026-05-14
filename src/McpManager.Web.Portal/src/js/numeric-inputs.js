import AutoNumeric from "autonumeric";
import $ from 'jquery';
import 'jquery-validation';
import 'jquery-validation-unobtrusive';


for(let el of document.querySelectorAll(".numeric")){
    new AutoNumeric(el, {
        unformatOnSubmit: true,
        digitGroupSeparator: ' ',
        decimalCharacter: ','
    });
}

// jQuery Validation rules for number
$.validator.addMethod('number', function (value, element, params) {
    return !isNaN(parseFloat(value.replace(' ', '').replace(',', '.')));
});
