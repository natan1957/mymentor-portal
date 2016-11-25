

(function () {
    shoppingCart.prototype = new _mymentor.Module;
    shoppingCart.prototype.base = _mymentor.Module.prototype;    

    function shoppingCart(name, settings) {
        this.name = name;
        this.settings = settings;
      
    };

    shoppingCart.prototype.init = function () {
        this.base.init.call(this);
    };

    shoppingCart.prototype.onLoad = function () {
        this.base.onLoad.call(this);
        m.initEvents();
    };

    var eh = {
        addToCart: function (e) {
            var itemType = e.dataset.itemShoppingCart;
            var dataModel = new DataModel();            
            
            var addToCartRequest = {
                ContentItemId: $(e).parent().data("item-id"),
                ContentItemType:$(e).data("item-shopping-cart")
            }
            $('body').css('cursor', 'wait');
            $(e).css('cursor', 'wait');
            dataModel.addToShoppingCart(addToCartRequest).done(function (d) {
                $('body').css('cursor', 'default');
                $(e).css('cursor', 'default');
                $(e).one("click", function () {
                    eh.addToCart(this);
                });

                if (d.IsAppUser) {
                    $("#dialog_appUser .title").text(d.ErrorMessage);
                    $("#dialog_appUser").dialog("option", "dialogClass", "dialog_white").dialog("open");
                    return;
                }

                //show error dialog
                if (d.ErrorMessage !== null) {
                    $("#dialog_orderError .title").text(d.ErrorMessage);
                    $("#dialog_orderError").dialog("option", "dialogClass", "dialog_white").dialog("open");
                    return;
                }

                // set dialog lesson/bundle or demo
                if (itemType == "demo-lesson") {
                    $("#dialog_orderDemo").dialog("option", "dialogClass", "dialog_white").dialog("open");
                }
                else if (itemType == "lesson" ||
                         itemType == "lesson+support" ||
                        itemType == "bundle" ||
                        itemType == "bundle+support") {

                    // set dialog text
                    if (itemType === "lesson" || itemType === "lesson+support") {
                        $("#dialog_addLeasson .center").text($("#dialog_addLeasson").data("lesson-text"));
                    }
                    else if (itemType === "bundle" || itemType === "bundle+support") {
                        $("#dialog_addLeasson .center").text($("#dialog_addLeasson").data("bundle-text"));
                    }

                    $("#dialog_addLeasson").dialog("option", "dialogClass", "dialog_white").dialog("open");
                }

                // increment users items count
                $(".numberOfProducts").text(d.UserPurchaseCount);
               
            });
        },
        closeDialog:function(e) {
            $(e).parents(".dialog_mentor").dialog("close");
        }
    }

    var m = {
        initEvents : function() {
            $("*[data-item-shopping-cart]").one("click",function() {
                eh.addToCart(this);
            });

            $("*[data-dialog-close").on("click", function() {
                eh.closeDialog(this);
            });
        }
    }
    _mymentor.addModule("shoppingCart", shoppingCart);
})();

