

(function () {
    querystring.prototype = new _mymentor.Module;
    querystring.prototype.base = _mymentor.Module.prototype;

    function querystring(name, settings) {
        this.name = name;
        this.settings = settings;
    };

    querystring.prototype.init = function () {
        this.base.init.call(this);
    };

    querystring.prototype.onLoad = function () {
       
    };

    querystring.prototype.get = function (name) {
        name = name.replace(/[\[]/, "\\[").replace(/[\]]/, "\\]");
        var regex = new RegExp("[\\?&]" + name + "=([^&#]*)"),
            results = regex.exec(location.search);
        return results === null ? "" : decodeURIComponent(results[1].replace(/\+/g, " "));
    };
  

    _mymentor.addModule("querystring", querystring);
})();

