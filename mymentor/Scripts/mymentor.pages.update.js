(function () {
    var _vm = null;
    update.prototype = new _mymentor.Page;
    update.prototype.base = _mymentor.Page.prototype;
    var _settings = null;

    function update(name, settings) {
        this.name = name;
        _settings = settings;
    };

    function viewModel() {
        var self = this;

        self.isValid = ko.observable(true);

        var selectedResidenceTitle = $("#residenceTitleSaved").val();
        var selectedResidenceId = $("#residenceIdSaved").val();

        if (selectedResidenceTitle.length == 0) {
            self.selectedResidenceName = ko.observable("בחר איזור הוראה");
        }
        else {
            self.selectedResidenceName = ko.observable(selectedResidenceTitle);
        }

        self.selectedResidenceId = ko.observable(selectedResidenceId);

        self.showResidenceView = function (event, sender) {           

            if ($("#residenceView").css("display") == "block") {
                $("#residenceView").slideUp(400);
            }
            else {
                $("#residenceView").slideDown(400);
                $(document).one("mouseup", function (e) {
                    var container = $("#residenceView");
                    if (!container.is(e.target)
                        && container.has(e.target).length === 0) {

                        $("#residenceView").slideUp(400);
                    }
                });
            }
            //  sender.target.contentEditable = true;
        };

        self.residenceBlur = function (event, sender) {
            if ($(sender.currentTarget).html() == "") {
                _vm.selectedResidenceId("");
                _vm.selectedResidenceName("");
            }
        };

        self.residencekeyDown = function (event, sender) {
            if (sender.keyCode != 8) //backspace
            {
                return false;
                //alert("keydown");
            }
            return true;
        };

        self.chooseImageFile = function (event, sender) {
            $("#fileInput").click();
        }

        self.imageFileSelected = function (event, sender) {
            var input = $("#fileInput")[0];

            if (input.files && input.files[0]) {
                var reader = new FileReader();

                reader.onload = function (e) {
                    $('#profileImage')
                        .attr('src', e.target.result).width(300);
                };

                reader.readAsDataURL(input.files[0]);
            }
        }

        self.checkAgent = function (agentName) {
            $('.btn_mentor ').css('cursor', 'wait');
            var dm = new DataModel();
            return dm.checkAgentExists(agentName);
        }

        self.submitAccountUpdateForm = function (event, sender) {
            var isValid = $("#accountUpdate").valid();
            if (isValid && self.checkTerms()) {
                $("#accountUpdate")[0].submit();

            } else {
                _vm.isValid(false);

            }
        }

        self.submitAccountAdminUpdateForm = function (event, sender) {
            var isValid = $("form").valid();
            if (isValid) {
                self.checkAgent($("#AdminData_AgentUserName").val()).done(function (d) {
                    $('.btn_mentor').css('cursor', 'pointer');
                    if (d == "True") {
                        $("form").submit();
                    } else {
                        $("#AdminData_AgentFullName").addClass("input-error");
                        alert(_settings.agentNotFoundMessage);
                    }
                });
            }
        }



        //self.selectedSugTagmul = ko.observable($("#selectedSugTagmul").val());

        //self.selectedSugTagmulChange = ko.computed(function (e) {
        //    var value = self.selectedSugTagmul();
        //    var fields = $("#selectedSugTagmul option:selected").data("fields");
        //    $("#mutav").hide();
        //    $("#bank").hide();
        //    $("#paypal").hide();
        //    $("#heshbon").hide();
        //    $("#irs").hide();
        //    $("#maam").hide();
        //    $.each(fields, function (index,value) {
        //        $("#" + value).show(100);
        //    });
        //});

        self.selectedDeviceType = ko.observable($("#deviceType").val());

        self.checkTerms = function () {
            var cbTerms = $('*[data-action="terms"]');
            var termsMessage = $('*[data-valmsg-for="ApproveTerms"]');
            if (!cbTerms.is(":checked")) {
                termsMessage.removeClass("field-validation-valid");
                termsMessage.addClass("field-validation-error");
                cbTerms.off("change");
                cbTerms.on("change", self.checkTerms);
                return false;
            }
            else {
                termsMessage.addClass("field-validation-valid");
                termsMessage.removeClass("field-validation-error");
                return true;
            }          
        }
        
    }

    var m= {
        setSugOsek: function (node) {
          
            $.each($("#SelectedSugOsek1").children(), function (index, item) {
                if ($(item).data("israeli") == undefined) {
                    return;
                }
                if ($(item).data("israeli") != node.data.data.israel) {
                    $(item).hide();
                    $(item).prop("selected", false);
                } else {
                    $(item).show();
                }
            });


        }
    }

    update.prototype.init = function () {
        this.base.init.call(this);
    };

    update.prototype.onLoad = function () {
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

        this.base.onLoad.call(this);

        var successMessage = $("#mtr-sucessMessage");
        $(successMessage).dialog("option", "dialogClass", "dialog_white").dialog("open");
        $('.ui-widget-overlay').off("click");

        var dataModel = new DataModel();
        dataModel.getResidences(_vm.selectedResidenceId()).done(function (data) {
            $.ui.dynatree.nodedatadefaults["icon"] = false;
            $("#residenceView").dynatree({
                checkbox: false,
                fx: { height: "toggle", duration: 200 },
                onActivate: function (node) {
                },
                children: JSON.parse(data),
                onPostInit: function () {
                    var selectedResidenceId = _vm.selectedResidenceId();
                    if (selectedResidenceId != null && selectedResidenceId.length > 0) {
                        this.activateKey(_vm.selectedResidenceId());
                        var selectedNode = $("#residenceView").dynatree("getActiveNode");
                        if (selectedNode != undefined) {
                            m.setSugOsek(selectedNode);
                        }
                    }
                },
                selectMode: 2,

                onClick: function (node, event) {
                    if (event.target.className != "dynatree-expander") {

                        var rootNode = node;
                        while (rootNode.parent.parent != null) {
                            rootNode = rootNode.parent;
                        }

                        var currentResidenceCountry = $("#ResidenceCountry").val();
                        if (eval(_settings.lockCountry.toLowerCase()) && currentResidenceCountry != rootNode.data.title) {
                            alert(_settings.countryLockedMessage);
                            return;
                        }

                        var residenceSelected = node.getLevel() ==3 || node.getLevel()==4;

                        if (node.getLevel() == 2) {
                            var childNodes = node.getChildren();
                            $.each(childNodes, function(index,cNode) {
                                cNode.toggleExpand();
                            });
                        }
                        if (node.getLevel() == 3) {
                            $("#residenceView").slideUp(200);
                            _vm.selectedResidenceName(node.data.title);
                            _vm.selectedResidenceId(node.data.key);
                            $("#CityOfResidence").val(""); 
                            $("#CityOfResidence_en_us").val("");                            
                        }
                        if (node.getLevel() == 4) {
                            $("#residenceView").slideUp(200);
                            _vm.selectedResidenceName(node.parent.data.title);
                            _vm.selectedResidenceId(node.parent.data.key);
                            $("#CityOfResidence").val(node.data.title);
                            $("#CityOfResidence_en_us").val(node.data.data.cityEn);
                            
                        }
                        else {
                          //  node.toggleExpand();
                            if (!node.data.isFolder) {
                                node.toggleExpand();
                            }
                        }

                        if (residenceSelected) {
                           
                            $("#ResidenceCountry").val(rootNode.data.title);
                        }
                        $("#SelectedSugOsek").val("");
                        m.setSugOsek(node);
                    }
                }
            });

            $("#residenceView").prepend("<div class=\"tree-clear-selection\" id=\"cleartree\">" + $("#teachingArea").data("clear-selection") + "</div>");
            $(".tree-clear-selection").bind("click", function () {
                _vm.selectedResidenceId("");
                $("#residenceView").slideUp(400);
                _vm.selectedResidenceName($("#teachingArea").data("placeholder"));
            });            
        });

        ko.applyBindings(_vm);

        $("#accountUpdate").validate().settings.ignore = "";
        $("#TeachesFromYear").mask("0000");

        $("#userGroups").bind("change", function(sender) {
            $("#userSubGroups>option").remove();
            var selectedValue = $(sender.currentTarget).val();
            var subGroups = $(sender.currentTarget).data("subgroups");
            var selectedSubGroup = $("#userSubGroups").data("selected-subgroup");

            $.each(subGroups, function(index, group) {
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

        var lastSugOsekValue = "";
        $("#SelectedSugOsek1").bind("click",function() {
                lastSugOsekValue = $(this).val();
            })
            .bind("change", function (sender) {
            if (_settings.lockSugOsek == "False") {
                var selectedValue = $(sender.currentTarget).val();
                $("#SelectedSugOsek").val(selectedValue);
            } else {
                alert(_settings.sugOsekLockedMessage);
                $(this).val(lastSugOsekValue);
            }
            });

        var lastSelectedCurrency = "";
        $("#UserPrefferences_SelectedCurrency").bind("click", function() {
            lastSelectedCurrency = $(this).val();
        }).bind("change",function(sender) {
            if (_settings.lockCurreny == "False") {
                var selectedValue = $(sender.currentTarget).val();
                $(this).val(selectedValue);
            } else {
                alert(_settings.currencyLockedMessage);
                $(this).val(lastSelectedCurrency);
            }
        });


        var lastSelectedsugNirut= "";
        $("#SelectedSugNirut").bind("click", function () {
            lastSelectedsugNirut = $(this).val();
        }).bind("change", function (sender) {
            if (_settings.lockSugNirut == "False") {
                var selectedValue = $(sender.currentTarget).val();
                $(this).val(selectedValue);
            } else {
                alert(_settings.sugNirutLockedMessage);
                $(this).val(lastSelectedsugNirut);
            }
        });
    }

    _mymentor.addPage("update", update);
})();