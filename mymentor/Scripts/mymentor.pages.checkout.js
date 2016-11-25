(function () {
    var _vm = null;
    checkout.prototype = new _mymentor.Page;
    checkout.prototype.base = _mymentor.Page.prototype;
    var _settings = null;

    function checkout(name, settings) {
        this.name = name;
        _settings = settings;
    };

    function viewModel() {
        var self = this;

        this.selectedCoupon = ko.observable();

        this.closeConfirmDialog = m.closeConfirmDialog;

        this.openAddCouponConfirmDialog = m.openAddCouponConfirmDialog;

        this.addCoupon = m.addCoupon;

    }

    var m= {
       closeConfirmDialog : function() {
           $(".dialog_mentor").dialog("close");
           _vm.selectedCoupon("");
       },
       openAddCouponConfirmDialog: function () {
           if (_vm.selectedCoupon().length > 0) {
               $("#dialog_confirmCoupon").dialog("option", "dialogClass", "dialog_white").dialog("open");
           }
       },
        addCoupon : function() {
            document.location.href = "/checkout/addcoupon/" + _vm.selectedCoupon();
        }
    }

    checkout.prototype.init = function () {
        this.base.init.call(this);
    };

    checkout.prototype.onLoad = function () {
        _vm = new viewModel();
        this.base.onLoad.call(this);
        
        ko.applyBindings(_vm);

        if ($("#dialog_error").length > 0) {
            $("#dialog_error").dialog("option", "dialogClass", "dialog_white").dialog("open");
        }

        $("#payBtn").click(function() {
            $(this).closest('form')[0].submit();
            return false;
        });
    }

    _mymentor.addPage("checkout", checkout);
})();