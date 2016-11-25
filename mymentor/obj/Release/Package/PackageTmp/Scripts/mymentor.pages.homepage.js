(function () {
    homepage.prototype = new _mymentor.Page;
    homepage.prototype.base = _mymentor.Page.prototype;

    var eh = {
        chooseContent: function (viewName) {
            var options = homePage.settings.userName.length > 0 ? { expires: 720 } : null;
            $.cookie('contentType', viewName, options);
            document.location.reload();
        }
    };

    function viewModel() {
        chooseContent = function (data, event) {
            eh.chooseContent(event.currentTarget.dataset.bannerView);
        };
    }

    var homePage;

    function homepage(name, settings) {
        homePage = this;
        this.name = name;
        this.settings = settings;
    };

    homepage.prototype.init = function () {
        this.base.init.call(this);
    };

    homepage.prototype.onLoad = function () {

        if (($.cookie('contentType') == undefined ||
            $.cookie('contentType') == null ||
            $.cookie('contentType') == "") && this.settings.showWorldsPicker) {

            $("#dialog_welcome").dialog("option", "dialogClass", "dialog_welcome_wrapper").dialog("open");
        }

        if (($.cookie('contentType') == undefined ||
            $.cookie('contentType') == null ||
            $.cookie('contentType') == "") &&
            (this.settings.singleWorldId != undefined &&
            this.settings.singleWorldId.length > 0)) {

            eh.chooseContent(this.settings.singleWorldId);
        }

        var vm = new viewModel();
        ko.applyBindings(vm, $("#dialog_welcome")[0]);
    }

    _mymentor.addPage("homepage", homepage);
})();

