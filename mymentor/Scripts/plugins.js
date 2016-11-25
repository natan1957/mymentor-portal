// Avoid `console` errors in browsers that lack a console.
(function() {
    var method;
    var noop = function () {};
    var methods = [
        'assert', 'clear', 'count', 'debug', 'dir', 'dirxml', 'error',
        'exception', 'group', 'groupCollapsed', 'groupEnd', 'info', 'log',
        'markTimeline', 'profile', 'profileEnd', 'table', 'time', 'timeEnd',
        'timeStamp', 'trace', 'warn'
    ];
    var length = methods.length;
    var console = (window.console = window.console || {});

    while (length--) {
        method = methods[length];

        // Only stub undefined methods.
        if (!console[method]) {
            console[method] = noop;
        }
    }
}());

// Place any jQuery/helper plugins in here.

$.fn.exists = function () {
    return this.length !== 0;
}

/* Hebrew initialisation for the UI Datepicker extension. */
/* Written by Amir Hardon (ahardon at gmail dot com). */
jQuery(function ($) {
    $.datepicker.regional['he'] = {
        clearText: 'נקה', clearStatus: '',
        closeText: 'X', closeStatus: '',
        prevText: '&#x3c;הקודם', prevStatus: '',
        prevBigText: '&#x3c;&#x3c;', prevBigStatus: '',
        nextText: 'הבא&#x3e;', nextStatus: '',
        nextBigText: '&#x3e;&#x3e;', nextBigStatus: '',
        currentText: 'היום', currentStatus: '',
        monthNames: ['ינואר', 'פברואר', 'מרץ', 'אפריל', 'מאי', 'יוני',
		'יולי', 'אוגוסט', 'ספטמבר', 'אוקטובר', 'נובמבר', 'דצמבר'],
        monthNamesShort: ['1', '2', '3', '4', '5', '6',
		'7', '8', '9', '10', '11', '12'],
        monthStatus: '', yearStatus: '',
        weekHeader: 'Sm', weekStatus: '',
        dayNames: ['ראשון', 'שני', 'שלישי', 'רביעי', 'חמישי', 'שישי', 'שבת'],
        dayNamesShort: ['א\'', 'ב\'', 'ג\'', 'ד\'', 'ה\'', 'ו\'', 'שבת'],
        dayNamesMin: ['ראשון', 'שני', 'שלישי', 'רביעי', 'חמישי', 'שישי', 'שבת'],
        //dayNamesMin: ['א\'','ב\'','ג\'','ד\'','ה\'','ו\'','שבת'],
        dayStatus: 'DD', dateStatus: 'DD, M d',
        dateFormat: 'dd/mm/yy', firstDay: 0,
        isRTL: true,
        initStatus: ''
    };

    $.datepicker.setDefaults($.datepicker.regional['he']);
});

function placeholderIsSupported() {
    var test = document.createElement('input');
    return ('placeholder' in test);
}


