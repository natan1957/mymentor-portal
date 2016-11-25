(function () {
    var _vm;
    register.prototype = new _mymentor.Page;
    register.prototype.base = _mymentor.Page.prototype;

    function register(name, settings) {
        this.name = name;
        this.settings = settings;                
    };

    function viewModel() {
        var self = this;
        self.selectedDeviceType = ko.observable($("#deviceType").val());

        self.isValid = ko.observable(true);
    };

    register.prototype.init = function () {
        this.base.init.call(this);
    };

    register.prototype.onLoad = function () {
        this.base.onLoad.call(this);

        _vm = new viewModel();

        _vm.selectedDeviceType.subscribe(function (newValue) {
            if (newValue.length >0)
            {
                if (newValue.indexOf("|") != -1) {
                    var value = newValue.split("|");
                    if (value[1] == "False")
                    {
                        var text = $("#deviceType").data("unsupported-text");
                        alert(text);
                    }
                }
            }            
        });

        ko.applyBindings(_vm);

       
        var successMessage = $("#mtr-sucessMessage");
        if (successMessage.length > 0)
        {
            $(successMessage).dialog("option", "dialogClass", "dialog_white").dialog("open");
            $('.ui-widget-overlay').off("click");
        }

        $("#Password").yapsm({
            dictionary: function () {
                return ["admin", "test"];
            }}).keyup(function () {
               if ($("#password_complexity").css("display") == 'none')
               {
                   $("#password_complexity").show();
               }
               $("#password_complexity").attr("class",this.complexity);
            });


        registerSubmit();

    }

    function registerSubmit() {
        $("#btnSubmit").one("click", function () {
            if ($('form').valid()) {
                $('form').submit();
            } else {
                registerSubmit();
                _vm.isValid(false);
            }
        });
    }

    _mymentor.addPage("register", register);
})();