(function () {
    var _vm = null;
    var _settings = null;
    updateStudent.prototype = new _mymentor.Page;
    updateStudent.prototype.base = _mymentor.Page.prototype;

    function updateStudent(name, settings) {
        this.name = name;
        _settings = settings;
    };

    function viewModel() {
        var self = this;
        self.selectedDeviceType = ko.observable($("#deviceType").val());
        self.isValid = ko.observable(true);
    }

    updateStudent.prototype.init = function () {
        this.base.init.call(this);
    };

    updateStudent.prototype.onLoad = function () {
        
        this.base.onLoad.call(this);

        var successMessage = $("#mtr-sucessMessage");
        if (successMessage.length > 0) {
            $(successMessage).dialog("option", "dialogClass", "dialog_white").dialog("open");
            $('.ui-widget-overlay').off("click");
        }

        var appuserMessage = $("#appuserMessage");
        if (appuserMessage.length > 0) {
            $(appuserMessage).dialog("option", "dialogClass", "dialog_white").dialog("open");
            //$('.ui-widget-overlay').off("click");
            $("#appuserMessage .btn_mentor").on("click", function () {
                $(appuserMessage).dialog("option", "dialogClass", "dialog_white").dialog("close");
                return false;
            });
        }

        _vm = new viewModel();
        _vm.selectedDeviceType.subscribe(function (newValue) {
            if (newValue.length > 0) {
                if (newValue.indexOf("|") != -1) {
                    var value = newValue.split("|");
                    if (value[1] == "False") {
                        var text = $("#deviceType").data("unsupported-text");
                        alert(text);
                    }
                }
            }
        });

        ko.applyBindings(_vm);

        $("#userGroups").bind("change", function (sender) {
            $("#userSubGroups>option").remove();
            var selectedValue = $(sender.currentTarget).val();
            var subGroups = $(sender.currentTarget).data("subgroups");
            var selectedSubGroup = $("#userSubGroups").data("selected-subgroup");

            $.each(subGroups, function (index, group) {
                if (group.ParentId == selectedValue) {
                    var option = $("<option></option>");
                    option.attr("value", group.Id);
                    option.text(group.Name);
                    if (selectedSubGroup == group.Id) {
                        option.attr("selected", "selected");
                    }
                    $("#userSubGroups").append(option);
                }
            });
        });
        $("#userGroups").trigger("change");


        $(".submit").click(function() {
            if ($('form').valid()) {
                 $('form').submit();
            } else {
                _vm.isValid(false);
            }
        });

        var lastSelectedCurrency = "";
        $("#UserPrefferences_SelectedCurrency").bind("click", function () {
            lastSelectedCurrency = $(this).val();
        }).bind("change", function (sender) {
            if (_settings.lockCurreny == "False") {
                var selectedValue = $(sender.currentTarget).val();
                $(this).val(selectedValue);
            } else {
                alert(_settings.currencyLockedMessage);
                $(this).val(lastSelectedCurrency);
            }
        });
        
        var lastSelectedcoutryOfResidence = "";
        $("#coutryOfResidence").bind("click", function () {
            lastSelectedcoutryOfResidence = $(this).val();
        }).bind("change", function (sender) {
            if (_settings.lockCountry == "False") {
                var selectedValue = $(sender.currentTarget).val();
                $(this).val(selectedValue);
            } else {
                alert(_settings.countryLockedMessage);
                $(this).val(lastSelectedcoutryOfResidence);
            }
        });
    }

    _mymentor.addPage("updateStudent", updateStudent);
})();