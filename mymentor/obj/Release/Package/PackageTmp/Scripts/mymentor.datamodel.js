function DataModel() {
    var self = this,
        getCategories = "/api/CategoriesAPI/GetCategories",
        getResidence = "/home/GetResidence",
        getLessonData = "/lessons/GetContentItemMetaData",
        setLessonData = "/lessons/SetContentItemMetaData",
        getConvertedCurrency = "/lessons/GetConvertedCurrency",
        setBundelData = "/lessons/AddUpdateBundleData",
        getBundleCreateData = "/lessons/getBundleCreateData",
        getBundleEditData = "/lessons/getBundleEditData",
        deleteBundle = "/lessons/DeleteBundle",
        getBundleMinimumPrice = "/lessons/GetBundleMinimumPrice",
        getCuponFirstPageData = "/cupon/getfirstPageData",
        submitCouponFirstPage = "/cupon/submitFirstPage",
        submitCouponSecondPage = "/cupon/submitSecondPage",
        getCouponSecondPageDataAfterPaymentCancelation = "/cupon/GetSecondPageDataAfterPaymentCancelation",
        getCouponThirdPageDataAfterPaymentIsComplete = "/cupon/PaymentValidationSuccess",
        addToShoppingCart = "/ShoppingCart/addToCart",
        checkAgentExists = "/account/CheckAgentExists";

    self.checkAgentExists = function(agentName) {
        return $.ajax(checkAgentExists, {
            type: "Get",
            cache: true,
            data: { agentName: agentName }
        });
    }

    self.getCategories = function () {
        return $.ajax(getCategories, {
            type: "Get",
            cache: true
        });
    }

    self.getResidences = function (selectedNodeId) {
        return $.ajax(getResidence, {
            type: "Get",
            cache: true,
            data: { selectedNodeId: selectedNodeId }
        });
    }

    self.getLessonData = function (id) {
        return $.ajax(getLessonData, {
            type: "Get",
            cache: false,
            data: { itemId: id }
        });
    }

    self.getConvertedCurrency = function (sourceCurrencyId, amount) {
        return $.ajax(getConvertedCurrency, {
            type: "Get",
            cache: false,
            data: { sourceCurrencyId: sourceCurrencyId, amount: amount }
        });
    }

    self.setLessonData = function (data) {
        return $.ajax(setLessonData, {
            type: "Post",
            cache: false,
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            data: data
        });
    }

    self.setBundleData = function (data) {
        return $.ajax(setBundelData, {
            type: "Post",
            cache: false,
            contentType: "application/json; charset=utf-8",
            dataType: "json",
            data: data
        });
    }

    self.getBundleCreateData = function (clipId) {
        return $.ajax(getBundleCreateData, {
            type: "Get",
            cache: false,
            data: { clipId: clipId }
        });
    }

    self.getBundleEditData = function (bundleId) {
        return $.ajax(getBundleEditData, {
            type: "Get",
            cache: false,
            data: { bundleId: bundleId }
        });
    }

    self.getBundleMinimumPrice = function (clipsInBundle) {
        var data = { ClipMinimalDataItemsJson: JSON.stringify(clipsInBundle) };
        return $.ajax(getBundleMinimumPrice, {
            type: "Post",
            cache: false,
            data: data
        });
    }

    self.deleteBundle = function (bundleId) {
        return $.ajax(deleteBundle, {
            type: "Get",
            cache: false,
            data: { bundleId: bundleId }
        });
    }

    self.getCuponFirstPageData = function (contentItemType, contentItemId) {
        return $.ajax(getCuponFirstPageData, {
            type: "Get",
            cache: false,
            data: { contentItemType: contentItemType, contentItemId: contentItemId }
        });
    }

    self.submitCouponFirstPage = function (email, discountPrice, contentItemId, contentItemType, includesSupport) {
        discountPrice = discountPrice == "" ? 0 : discountPrice;
        var data = { email: email, discountPrice: discountPrice, contentItemId: contentItemId, contentItemType: contentItemType, includesSupport: includesSupport };

        return $.ajax(submitCouponFirstPage, {
            type: "Post",
            dataType: "json",
            cache: false,
            data: data
        });
    }

    self.submitCouponSecondPage = function(couponVm) {
        var data ={
            originalPrice: couponVm.originalPrice,
            discountPrice: couponVm.discountPrice,
            amountForPayment:couponVm.amountForPayment,
            currentUrl: couponVm.currentUrl
        };

        return $.ajax(submitCouponSecondPage, {
            type: "Post",
            dataType: "json",
            cache: false,
            data: data
        });
    }

    self.getCouponSecondPageDataAfterPaymentCancelation = function () {
        return $.ajax(getCouponSecondPageDataAfterPaymentCancelation, {
            type: "Get",
            cache: false,
            data: { }
        });
    }

    self.getCouponThirdPageDataAfterPaymentIsComplete = function (paymentId,payerId) {
        return $.ajax(getCouponThirdPageDataAfterPaymentIsComplete, {
            type: "Get",
            cache: false,
            data: {paymentId:paymentId, payerId:payerId}
        });
    }

    self.addToShoppingCart = function (itemData) {
        return $.ajax(addToShoppingCart, {
            type: "Get",
            cache: false,
            data: itemData
        });
    }
}