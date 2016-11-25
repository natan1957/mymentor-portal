
var tooltipOptions, tooltipClickableOptions;
$(document).ready(function () {

    $('input, textarea').placeholder();
    $.widget('ui.dialog', jQuery.extend({}, jQuery.ui.dialog.prototype, {
        _title: function (titleBar) {
            titleBar.html(this.options.title || '&#160;');
        }
    }));

    $.ui.dialog.prototype._focusTabbable = function () { };

    $(".dialog_mentor").dialog({
        autoOpen: false,
        modal: true,
        hide: "fade",
        show: 'fade',
        width: 'auto',
        open: function () {
            //$('.ui-widget-overlay').bind('click', function () {
            //    $(".ui-dialog-titlebar-close").trigger('click');

            //})
            if (!placeholderIsSupported()) {
                $(this).find('input, textarea').focus().blur();

                $(".ui-datepicker").stop().hide();
                $(".icon_calendar_input").blur().datepicker('hide');
            }
        }
    });

    $(document).on("click", ".closeDialog", function (evt) {
        $(evt.target).closest('.ui-dialog').find('.ui-dialog-titlebar-close').click();
    });
    

    $(".icon_calendar_input").datepicker({

    });
    
    tooltipOptions = {
        position: {
            my: "center bottom-15",
            at: "center top",
            using: function (position, feedback) {
                $(this).css(position);
                $("<div>")
                .addClass("arrowToolTip")
                .addClass(feedback.vertical)
                .addClass(feedback.horizontal)
                .appendTo(this);
            }
        }
    };

    tooltipClickableOptions = {
        position: {
            my: "center bottom-5",
            at: "center top",
            using: function (position, feedback) {
                $(this).css(position);
                $("<div>")
                .addClass("arrowToolTip")
                .addClass(feedback.vertical)
                .addClass(feedback.horizontal)
                .appendTo(this);
                $("<div>")
                .addClass("closeToolTip")
                .appendTo(this);
            }
        },
        open: function (event, ui) {

            
        }
    };

    $(".blackTooltip").tooltip(tooltipOptions)
    .tooltip("option", "tooltipClass", "blackTooltip");

    $(".whiteTooltip").tooltip(tooltipOptions)
    .tooltip("option", "tooltipClass", "whiteTooltip");
    
    $(".whiteTooltipClickable").tooltip(tooltipClickableOptions)
   .tooltip("option", "tooltipClass", "whiteTooltip draggableTooltip");

    var isCtrlPressed = false;
    $(".whiteTooltipClickable")//.off("mouseover focusin")
      .on("click", function (e) {
          var _this = $(this);
          var _isTruncated = _this.hasClass("Truncated")
          
          if (_this.siblings('.hiddenToolTip').length > 0) {
              if (!_isTruncated) {
                  _this.attr("title", "");
              }

              else {
                  _this.tooltip("close");
                  _this.data('TruncatedText', _this.attr("title"));
                  _this.tooltip("destroy");
                  _this.attr("title", "");
                  _this.tooltip(tooltipClickableOptions).tooltip("option", "tooltipClass", "whiteTooltip draggableTooltip");
              }
              
              _this.tooltip("option", "content", function () {
                  return _this.siblings('.hiddenToolTip').first().html();
              });
          }

          isCtrlPressed = false
          if (e.ctrlKey) {
              isCtrlPressed = true;
          }

          if (!isCtrlPressed) {
              var _parent = _this.parent().attr('class');
              $('.' + _parent + ' .openToolTip').tooltip("close").removeClass('openToolTip');
          }

          _this.addClass('openToolTip');
          _this.tooltip("open");
          $(".draggableTooltip").draggable({
              cancel: '.ui-tooltip-content, .closeToolTip',
              start: function (event, ui) {
                  var left = parseInt($(this).css('left'), 10);
                  left = isNaN(left) ? 0 : left;
                  var top = parseInt($(this).css('top'), 10);
                  top = isNaN(top) ? 0 : top;
                  recoupLeft = left - ui.position.left;
                  recoupTop = top - ui.position.top;
              },
              drag: function (event, ui) {
                  ui.position.left += recoupLeft;
                  ui.position.top += recoupTop;
              }
          });
          _this.off("mouseleave focusout");
          return false;

      });

    $(document.body).on('click', '.closeToolTip', function () {
        var _this = $(this);
        var sourceTooltip = $(".openToolTip[aria-describedby =" + _this.closest('.ui-tooltip').attr("id") + "]");
        sourceTooltip.tooltip("close").removeClass('openToolTip');
        //_this.on("mouseleave focusout");

        var _isTruncated = sourceTooltip.hasClass("Truncated");
        if (_isTruncated) {
            sourceTooltip.tooltip("destroy");
            sourceTooltip.attr("title", sourceTooltip.data('TruncatedText'));
            sourceTooltip.tooltip(tooltipOptions);
            sourceTooltip.tooltip("option", "tooltipClass", "whiteTooltip");
            
        }

        

        //$(".openToolTip").each(function (i) {
        //    $(this).tooltip("close");
        //    $(this).removeClass('openToolTip');
        //});
        return false;
    });


    $(document).click(function (e) {
        if ($(e.target).not(".openedWindow").length !== 0) {
            $(".clicked").click();
        }
    });

 	$(".dropdown_change").click(function (e) {
        if ($(this).hasClass('clicked')) {
            $(this).removeClass('clicked');
            $(this).tooltip("enable");
        }
        else {
            $(this).addClass('clicked');
            $(this).tooltip("disable");
        }

        e.stopPropagation();
    });







   
});


function OpenToolTips() {
    $(".blackTooltip, .whiteTooltip").tooltip(tooltipOptions).tooltip("open");
}

$.fn.exists = function () {
    return this.length !== 0;
}

function placeholderIsSupported() {
    var test = document.createElement('input');
    return ('placeholder' in test);
}

