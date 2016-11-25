

(function () {
    createCupon.prototype = new _mymentor.Module;
    createCupon.prototype.base = _mymentor.Module.prototype;
    var cuponData = {};

    function createCupon(name, settings) {
        this.name = name;
        this.settings = settings;
      
    };

    createCupon.prototype.init = function () {
        this.base.init.call(this);
    };

    createCupon.prototype.onLoad = function () {
        this.base.onLoad.call(this);

        var qs = new _mymentor.modules.querystring();
        if(qs.get("ppcancel") === "true")
        {
            $.blockUI({ message: $("#dialog_coupon").data("wait") });
            eh.get2pageDataAfterPaymentCancelation();
        }

        if (qs.get("ppsuccess") === "true") {
            $.blockUI({ message: $("#dialog_coupon").data("wait") });
            eh.get3pageAfterpaymentSuccess();
        }

        $(".editLessonCoupon").click(function (e) {
            eh.clearPrevData();

            var contentItemType = e.currentTarget.dataset.contentItemType;
            var contentItemId = e.currentTarget.dataset.contentItemId;
            cuponData.itemId = contentItemId;
            cuponData.contentItemType = contentItemType;            
            
            $("#dialog_coupon").dialog("option", "dialogClass", "dialog_couponLesson dialog_inputs").dialog("open");
            $("#dialog_couponStep2,#dialog_couponStep3,#dialog_error").hide();
            $("#dialog_couponStep1").show(function () {
                $("#dialog_coupon").block({
                    message: $("#dialog_coupon").data("wait"),
                    overlayCSS:
                    {
                        opacity: 1,
                        backgroundColor: "#e0dfdc"
                    }
                });
                eh.getFirstPage(contentItemType, contentItemId);
            });
        });

        $("#couponStep1").click(function () {
            var email = $("#couponStudentEmailAddress").val();
            var discountPrice = $("#couponDiscountPrice").val();
            var includesSupport = $("#supportIncluded").is(':checked');
            eh.submitFirstPage(email, discountPrice, cuponData.itemId, cuponData.contentItemType, includesSupport);
        });

        $("#couponStep2").click(function () {
            eh.submitSecondPage();
        });

        $("#couponBackTo1").on("click", eh.goBackToPage1);

        $("#dialog_coupon").on("dialogclose", function (event, ui) {
            cuponData = {};
        });

        $("#supportIncluded").on("change", function (event) {
            var showSupport = event.currentTarget.checked;
            if (showSupport) {
                $("#dialog_coupon").find(".lessonPrice").hide();
                $("#dialog_coupon").find(".lessonPriceWithSupport").show();
                $("#dialog_coupon").find(".teacherDiscountPrice").hide();
                $("#dialog_coupon").find(".teacherDiscountPriceWithSupport").show();
            } else {
                $("#dialog_coupon").find(".lessonPrice").show();
                $("#dialog_coupon").find(".lessonPriceWithSupport").hide();
                $("#dialog_coupon").find(".teacherDiscountPrice").show();
                $("#dialog_coupon").find(".teacherDiscountPriceWithSupport").hide();
            }
        });
    };

    var eh = {
        clearPrevData : function() {
            $("#dialog_coupon").find(".lessonHeaderText").text("");
            $("#dialog_coupon").find(".lessonHeaderText2").text("");
            $("#dialog_coupon").find(".lessonPrice").text("");
            $("#dialog_coupon").find(".lessonPriceWithSupport").text("");
            $("#dialog_coupon").find(".teacherDiscountPrice").text("");
            $("#dialog_coupon").find(".teacherDiscountPriceWithSupport").text("");
            $("#dialog_coupon").find(".whiteTooltip").attr("title", "");
            $("#couponStudentEmailAddress").text();
            $("#couponDiscountPrice").text();
            $("#studentValidationError").text("");
            $("#couponGeneralError").text("");
            $("#couponPriceError").text("");
        },
        getFirstPage: function (contentItemType, contentItemId) {
            new DataModel()
                .getCuponFirstPageData(contentItemType, contentItemId)
                .done(function (e) {
                    $("#dialog_coupon").find(".lessonHeaderText").text(e.ContentItemDetails.NamePart1);
                    $("#dialog_coupon").find(".lessonHeaderText2").text(e.ContentItemDetails.NamePart2);
                    $("#dialog_coupon").find(".lessonPrice").text(e.ContentItemDetails.FormattedOriginalPrice);
                    $("#dialog_coupon").find(".lessonPriceWithSupport").text(e.ContentItemDetails.FormattedSupportPrice);
                    $("#dialog_coupon").find(".whiteTooltip").attr("title", e.HelpText);
                    $("#dialog_coupon").find(".teacherDiscountPrice").text(e.TeacherData.FormattedTeacherDiscountPrice);
                    $("#dialog_coupon").find(".teacherDiscountPriceWithSupport").text(e.TeacherData.FormattedTeacherDiscountPriceWithSupport);
                    

                    var lessonOrBundleSpan = $("#dialog_coupon").find(".lessonOrBundleLabel");                    
                    lessonOrBundleSpan.text(contentItemType == "lesson" ? lessonOrBundleSpan.data("lesson") : lessonOrBundleSpan.data("bundle"));

                    var includesSupport = $("#supportIncluded").is(':checked');
                    m.setCouponData(e);
                    $("#dialog_coupon").unblock({});
                });
        },       
        submitFirstPage: function (email, discountPrice, contentItemId, contentItemType, includesSupport) {
            if (isNaN(discountPrice)) {
                $("#couponPriceError").text($("#couponDiscountPrice").data("numeric-val-error"));
                return;
            } else {
                $("#couponPriceError").text("");
            }
            $("#dialog_coupon").block({
                message: $("#dialog_coupon").data("wait"),
                overlayCSS:
                {
                    opacity: 1,
                    backgroundColor: "#e0dfdc"
                }
            });
            cuponData.Email = email;
            cuponData.DiscountPrice = discountPrice;
            cuponData.IncludesSupport = includesSupport;
            new DataModel()
                .submitCouponFirstPage(email, discountPrice, contentItemId, contentItemType, includesSupport)
                .done(function (e) {
                    if (e.CouponErrors.UserError != null) {
                        $("#studentValidationError").text(e.CouponErrors.UserError);
                    } else {
                        $("#studentValidationError").text("");
                    }

                    if (e.CouponErrors.PriceError != null) {
                        $("#couponPriceError").text(e.CouponErrors.PriceError);
                    } else {
                        $("#couponPriceError").text("");
                    }

                    if (e.CouponErrors.GeneralError != null) {
                        $("#couponGeneralError").text(e.CouponErrors.GeneralError);
                    } else {
                        $("#couponGeneralError").text("");
                    }
                    
                    if (!e.CouponErrors.HasErrors) {
                        m.setCouponData(e);

                        if (e.CouponId == null) {
                            var couponExists = e.CouponExists;
                            if (couponExists) {
                                var keepGoing = confirm($("#dialog_couponStep1").data("coupon-exists"));
                                if (keepGoing) {
                                    eh.goToPage2();
                                }
                            } else {
                                eh.goToPage2();
                            }

                        } else {
                            eh.goToPage3("#dialog_couponStep1");
                        }
                    }

                $("#dialog_coupon").unblock();
            });
        },
        submitSecondPage: function () {
            $("#dialog_coupon").block({
                message: $("#dialog_coupon").data("wait"),
                overlayCSS:
                {
                    opacity: 1,
                    backgroundColor: "#e0dfdc"
                }
            });
            var couponVm = {
                originalPrice: cuponData.OriginalPrice,
                discountPrice: cuponData.DiscountPrice,
                amountForPayment: cuponData.AmountForPayment,
                currentUrl:document.location.href
            };
            new DataModel()
                .submitCouponSecondPage(couponVm)
                .done(function (e) {
                if (e.CouponErrors.GeneralError != null) {
                    eh.showErrorPage("#dialog_couponStep2", e.CouponErrors.GeneralError);
                    return;
                }
                if (e.PaymentApprovalUrl != null && e.PaymentApprovalUrl.length > 0) {
                        document.location.href = e.PaymentApprovalUrl;
                        return;
                    }
                    cuponData.CouponValidUntil = e.CouponValidUntil;
                    cuponData.CouponId = e.CouponId;
                    eh.goToPage3();
                    $("#dialog_coupon").unblock({});
                });
        },
        goBackToPage1: function () {
            if (cuponData.UserName !== undefined) {
                $("#couponStudentEmailAddress").val(cuponData.UserName);
            }

            if (cuponData.DiscountPrice !== undefined) {
                $("#couponDiscountPrice").val(cuponData.DiscountPrice);
            }

            $("#dialog_coupon").find(".lessonPrice").text(cuponData.FormattedOriginalPrice);
            $("#dialog_coupon").find(".lessonPriceWithSupport").text(cuponData.FormattedSupportPrice);
            $("#dialog_coupon").find(".whiteTooltip").attr("title", cuponData.HelpText);
            $("#dialog_coupon").find(".teacherDiscountPrice").text(cuponData.FormattedTeacherDiscountPrice);
            $("#dialog_coupon").find(".teacherDiscountPriceWithSupport").text(cuponData.FormattedTeacherDiscountPriceWithSupport);

            $("#dialog_couponStep2").fadeOut(200, function () { $("#dialog_couponStep1").fadeIn(); });
        },
        goToPage2: function () {
            $("#dialog_couponStep1").fadeOut(200, function () { $("#dialog_couponStep2").fadeIn(); });
            $(".couponFor").html("&lrm;" + cuponData.UserFullName);
            $(".couponCurrentPrice").html("&lrm;" + cuponData.OriginalPriceForDisplay);
            $(".regularDiscountPrice").html("&lrm;" + cuponData.regularDiscountPrice);
            $(".regularPriceForStudent").html("&lrm;" + cuponData.Currency.CurrencySymbol + cuponData.DiscountPrice);

            $("#dialog_coupon").find(".whiteTooltip").attr("title", cuponData.HelpText);
            $("#dialog_coupon").find(".gapToPay").html("&lrm;" + cuponData.FormattedGapToPay);
            $("#dialog_coupon").find(".willBeChargedFromAccount").html("&lrm;" + cuponData.FormattedWillBeChargedFromAccount);
            $("#dialog_coupon").find(".amountForPayment").html("&lrm;" + cuponData.FormattedAmountForPayment);
            $("#dialog_coupon").find(".ballance").html("&lrm;" + cuponData.FormattedBalance);
            
            if (cuponData.AmountForPayment > 0) {
                $("#couponStep2").text("");
                $("#couponStep2").attr("class", "paymentBtn floatL");
                $("#paypallogo").show();
            } else {
                $("#couponStep2").text($("#couponStep2").data("approval"));
                $("#couponStep2").attr("class", "btn_mentor floatL");
                $("#paypallogo").hide();
            }
            
           
        },
        goToPage3: function (currentStepId) {
            if (currentStepId != undefined) {
                $(currentStepId).fadeOut(200, function() { $("#dialog_couponStep3").fadeIn(); });
            } else {
                $("#dialog_couponStep2").fadeOut(200, function () { $("#dialog_couponStep3").fadeIn(); });
            }

            $(".couponNumber").text(cuponData.CouponId);
            $(".couponSentTo").text($(".couponSentTo").text().replace('{0}', cuponData.UserFullName));
            $(".couponValidUntil").text(cuponData.CouponValidUntil);
        },
        get2pageDataAfterPaymentCancelation: function () {
            m.setRedirectUrl("ppcancel");

            new DataModel().getCouponSecondPageDataAfterPaymentCancelation().done(function (e) {
               
                m.setCouponData(e);
                $.unblockUI();

                $("#dialog_coupon").dialog("option", "dialogClass", "dialog_couponLesson dialog_inputs").dialog("open");

                if (e.CouponErrors.GeneralError != null) {
                    eh.showErrorPage("#dialog_couponStep2", e.CouponErrors.GeneralError);

                    return;
                }

                $("#dialog_coupon").find(".lessonHeaderText").text(e.ContentItemDetails.NamePart1);
                $("#dialog_coupon").find(".lessonHeaderText2").text(e.ContentItemDetails.NamePart2);

                $("#dialog_couponStep3").hide();
                $("#dialog_couponStep2").show();
                eh.goToPage2();
            });
        },
        get3pageAfterpaymentSuccess: function () {

            m.setRedirectUrl("ppsuccess");
            var qs = new _mymentor.modules.querystring();
            var paymentId = qs.get("paymentId");
            var payerId = qs.get("PayerID");

            new DataModel().getCouponThirdPageDataAfterPaymentIsComplete(paymentId,payerId).done(function (e) {

                m.setCouponData(e);
                $.unblockUI();

                $("#dialog_coupon").dialog("option", "dialogClass", "dialog_couponLesson dialog_inputs").dialog("open");

                if (e.CouponErrors.GeneralError != null) {
                    eh.showErrorPage("#dialog_couponStep2", e.CouponErrors.GeneralError);

                    return;
                }

                $("#dialog_coupon").find(".lessonHeaderText").text(e.ContentItemDetails.NamePart1);
                $("#dialog_coupon").find(".lessonHeaderText2").text(e.ContentItemDetails.NamePart2);

                $("#dialog_couponStep2").hide();
                $("#dialog_couponStep3").show();

                eh.goToPage3("#dialog_couponStep1");
            });
        },
        showErrorPage: function (currentStepId, errorText) {
            $("#dialog_coupon").unblock({});
            $("#dialog_couponStep1").hide();
            $("#dialog_couponStep2").hide();
            $("#dialog_couponStep3").hide();
            $("#dialog_error").fadeIn();
            //$(currentStepId).fadeOut(200, function () {});
            $("#couponErrorText").text(errorText);
        }
    }

    var m = {
        setCouponData: function (e) {
            if (e == null)return;
            cuponData.DiscountType = e.DiscountType;
            cuponData.Currency = e.SelectedCurrency;
            cuponData.UserFullName = e.CouponStudentDetails.StudentFullName;
            cuponData.UserId = e.CouponStudentDetails.StudentUserId;
            cuponData.UserName = e.CouponStudentDetails.userName;
            cuponData.Email = e.CouponStudentDetails.StudentEmailAddress;
            cuponData.FormattedGapToPay = e.TeacherData.FormattedGapToPay;
            cuponData.FormattedWillBeChargedFromAccount = e.TeacherData.FormattedWillBeChargedFromAccount;
            cuponData.FormattedAmountForPayment = e.TeacherData.FormattedAmountForPayment;
            cuponData.AmountForPayment = e.TeacherData.AmountForPayment;
            cuponData.FormattedBalance = e.TeacherData.FormattedBalance;
            cuponData.ContentItemCurrency = e.ContentItemDetails.Currency;
            cuponData.BundleClipIds = e.ContentItemDetails.BundleClipIds;
            cuponData.FormattedOriginalPrice = e.ContentItemDetails.FormattedOriginalPrice;
            cuponData.OriginalPrice = e.ContentItemDetails.OriginalPrice;
            cuponData.IncludesSupport = e.IncludesSupport;
            cuponData.regularDiscountPrice = cuponData.IncludesSupport ? e.TeacherData.FormattedTeacherDiscountPriceWithSupport : e.TeacherData.FormattedTeacherDiscountPrice;
            cuponData.OriginalPriceForDisplay = cuponData.IncludesSupport ? e.ContentItemDetails.FormattedSupportPrice : e.ContentItemDetails.FormattedOriginalPrice;
            cuponData.CouponId = e.CouponId;
            cuponData.CouponValidUntil = e.CouponValidUntil;
            cuponData.DiscountPrice = e.CouponDiscountPrice;
            cuponData.FormattedSupportPrice = e.ContentItemDetails.FormattedSupportPrice;
            cuponData.HelpText = e.HelpText;
            cuponData.FormattedTeacherDiscountPrice = e.TeacherData.FormattedTeacherDiscountPrice;
            cuponData.FormattedTeacherDiscountPriceWithSupport = e.TeacherData.FormattedTeacherDiscountPriceWithSupport;
        },
        setRedirectUrl : function(upToUrlToken) {
            $("#dialog_coupon").on("dialogclose", function (event, ui) {
                cuponData = {};
                var upTo = document.location.href.lastIndexOf(upToUrlToken);
                document.location.href = document.location.href.substring(0, upTo - 1);
            });
        }
    }
    _mymentor.addModule("createCupon", createCupon);
})();

