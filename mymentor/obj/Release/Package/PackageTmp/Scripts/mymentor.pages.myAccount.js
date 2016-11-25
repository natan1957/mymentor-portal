(function () {
    myAccount.prototype = new _mymentor.Page;
    myAccount.prototype.base = _mymentor.Page.prototype;
    var _self;

    function myAccount(name, settings) {
        _self = this;
        this.name = name;
        this.settings = settings;
    };

    myAccount.prototype.init = function () {
        this.base.init.call(this);
    };

    myAccount.prototype.onLoad = function () {
        if (localStorage.getItem("myAccount.nis") == "true") {
            $("#currencyCheckBox").attr("checked", "checked");
        }
        
        m.convertCurrency.call($("#currencyCheckBox")[0]);

        $('.MyAccountTypeItemPadding, .MyAccountUserItemPadding, .MyAccountActionItemPadding, .MyAccountItemPadding, .MyAccountNoteItemPadding').dotdotdot({
            //	configuration goes here
            watch: true,
            height: 20,
            tolerance: 7,
            callback: function (isTruncated, orgContent) {
                var _isTruncated = isTruncated;
                if (_isTruncated == true) {
                    if (orgContent[1] !== undefined && orgContent[1].localName == "strong") {
                        $(this).addClass("Truncated").attr("title", orgContent[1].innerHTML+ orgContent[2].data).tooltip(tooltipOptions);
                    } else {
                        $(this).addClass("Truncated").attr("title", orgContent[0].data).tooltip(tooltipOptions);
                    }
                    
                    $(this).tooltip("option", "tooltipClass", "whiteTooltip");

                    //$(this).append(orgContent);

                    //

                }
            }
        });

        var isTextShortened = true;
        $("#toggleTextBtn").click(function (e) {
            var _this = $(this);
            if (isTextShortened) {
                $('.Truncated').trigger('destroy').tooltip("destroy").attr("title", "").removeClass("Truncated whiteTooltip");
                $(".whiteTooltipClickable").tooltip(tooltipClickableOptions)
.tooltip("option", "tooltipClass", "whiteTooltip draggableTooltip");

                isTextShortened = false;
                _this.text(_self.settings.collapseText);
            }

            else {
                $('.MyAccountTypeItemPadding, .MyAccountUserItemPadding, .MyAccountActionItemPadding, .MyAccountItemPadding, .MyAccountNoteItemPadding').dotdotdot({
                    watch: true,
                    height: 20,
                    tolerance: 7,
                    callback: function (isTruncated, orgContent) {
                        if (isTruncated) {
                            $(".whiteTooltipClickable").tooltip(tooltipClickableOptions)
.tooltip("option", "tooltipClass", "whiteTooltip draggableTooltip");

                            $(this).addClass("Truncated").attr("title", orgContent[0].data).tooltip(tooltipOptions);
                            $(this).tooltip("option", "tooltipClass", "whiteTooltip");
                        }
                    }
                });
                isTextShortened = true;
                _this.text(_self.settings.expandText);
            }
        });

        $(document.body).on('click', '.eventUpdate', function (e) {
            var _this = $(this);
            $("#eventCodeInput").val(_this.text());
            if (!e.ctrlKey) {
                _this.closest('.ui-tooltip').find(".closeToolTip").click();
            }
            return false;
        });

        $(document.body).on('click', '.myAccountUser', function (e) {
            var _this = $(this);
            $("#myAccountUserName").val(_this.text());
            if (!e.ctrlKey) {
                _this.closest('.ui-tooltip').find(".closeToolTip").click();
            }
            return false;
        });

        $(document.body).on('click', '.myAccountDateUpdate', function (e) {
            var _this = $(this);                        
            $(".myAccountDates input").datepicker("setDate", _this.text());
            if (!e.ctrlKey) {
                _this.closest('.ui-tooltip').find(".closeToolTip").click();
            }
            return false;
        });

        $(document.body).on('click', "#showResults", function (e) {
            var startDate = $("#startDate").datepicker("getDate");
            var endDate = $("#endDate").datepicker("getDate");

            if (startDate > endDate && endDate != null) {
                alert(_self.settings.startDateBiggerAlert);
            } else {
                $("#myAccountForm").submit();
            }            
        });

        $(document.body).on('click', '#order', function (e) {
            $("#orderval").val(qs.get("asc").toLowerCase() == "true" || qs.get("asc").length === 0 ? false : true);
            $("#myAccountForm").submit();
        });

        $(document.body).on('change', "#currencyCheckBox", function (e) {
            localStorage.setItem("myAccount.nis", this.checked);
            m.convertCurrency.call(this);
        });

        var qs = new _mymentor.modules.querystring();
        if (qs.get("asc").toLowerCase() === "false") {
            $("#order").removeClass("upArrow");
        } else {
            $("#order").addClass("upArrow");
        }
    }

    var m= {
        convertCurrency: function () {
            if (this.checked) {
                $.each($("[data-amount-nis]"), function (index, item) {
                    var nis = $(item).data("amount-nis");
                    $(item).attr("data-amount", $(item).html());
                    $(item).html(nis);
                });
            } else {
                $.each($("[data-amount-nis]"), function (index, item) {
                    var amount = $(item).data("amount");
                    $(item).html(amount);
                });
            }
        }
    }

    
    _mymentor.addPage("myAccount", myAccount);
})();

