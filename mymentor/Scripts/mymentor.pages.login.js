(function () {
    login.prototype = new _mymentor.Page;
    login.prototype.base = _mymentor.Page.prototype;

    function login(name, settings) {
        this.name = name;
        this.settings = settings;
    };

    login.prototype.init = function () {
        this.base.init.call(this);
    };

    login.prototype.onLoad = function () {        
        this.base.onLoad.call(this);

        $("input").keypress(function (e) {
            kCode = e.keyCode || e.charCode //for cross browser
            if (kCode == 13) {
                var form = $('form');
                if (form.valid()) {
                    $('form').submit();
                }                
                return false;
            }
        });
    }

    _mymentor.addPage("login", login);
})();

