(function () {
    var vm;
    var vmSearchLessons;
    updateBundle.prototype = new _mymentor.Module;
    updateBundle.prototype.base = _mymentor.Module.prototype;    

    function bundleUpdateViewModel(data) {
        if (data == null) data = new Object();
        var self = this;
        this.isValid = ko.observable(true);
        this.ObjectId = ko.observable(data.ObjectId);
        this.ObjectIdFixed = ko.observable(data.ObjectId);
        this.BundleName = ko.observable(data.BundleName);
        this.BundleClips = ko.observableArray(data.BundleClips);
        this.CategoryName1 = ko.observable(data.CategoryName1);
        this.CategoryName2 = ko.observable(data.CategoryName2);
        this.CategoryName3 = ko.observable(data.CategoryName3);
        this.CategoryName4 = ko.observable(data.CategoryName4);

        this.Category1Values = ko.observableArray(data.Category1Values);
        this.Category2Values = ko.observableArray(data.Category2Values);
        this.Category3Values = ko.observableArray(data.Category3Values);
        this.Category4Values = ko.observableArray(data.Category4Values);

        this.SelectedCategory1Value = ko.observable(data.SelectedCategory1Value);
        this.SelectedCategory2Value = ko.observable(data.SelectedCategory2Value);
        this.SelectedCategory3Value = ko.observable(data.SelectedCategory3Value);
        this.SelectedCategory4Value = ko.observable(data.SelectedCategory4Value);

        this.getTextFromDropdown = function (selectedValue, values) {
            if (selectedValue == undefined ||                
                selectedValue.length == 0)
                return "";
                
            var selectedId = selectedValue;
            var text = $.grep(values, function (obj) {
                return obj.Key === selectedId;
            });

            if (text.length > 0) {
                return text[0].Value;
            }
            return "";
        }
        this.SelectedCategory1TextComputed = ko.computed(function() {
            return self.getTextFromDropdown(self.SelectedCategory1Value(), self.Category1Values());
        },this);
        this.SelectedCategory2TextComputed = ko.computed(function () {
            return self.getTextFromDropdown(self.SelectedCategory2Value(), self.Category2Values());
        }, this);
        this.SelectedCategory3TextComputed = ko.computed(function () {
            return self.getTextFromDropdown(self.SelectedCategory3Value(), self.Category3Values());
        }, this);
        this.SelectedCategory4TextComputed = ko.computed(function () {
            return self.getTextFromDropdown(self.SelectedCategory4Value(), self.Category4Values());
        }, this);

        this.Category1VisibleComputed = ko.computed(function() {
            return self.CategoryName1() !== undefined && self.CategoryName1() !=null && self.CategoryName1().length > 0;
        });
        this.Category2VisibleComputed = ko.computed(function () {
            return self.CategoryName2() !== undefined && self.CategoryName2() != null &&  self.CategoryName2().length > 0;
        });
        this.Category3VisibleComputed = ko.computed(function () {
            return self.CategoryName3() !== undefined && self.CategoryName3() != null && self.CategoryName3().length > 0;
        });
        this.Category4VisibleComputed = ko.computed(function () {
            return self.CategoryName4() !== undefined && self.CategoryName4() != null && self.CategoryName4().length > 0;
        });

        this.Created = ko.observable(data.Created);
        this.Updated = ko.observable(data.Updated);
        this.Version = ko.observable(data.Version);

        this.Remarks_he_il = ko.observable(data.Remarks_he_il);
        this.Remarks_en_us = ko.observable(data.Remarks_en_us);
        this.Description_he_il = ko.observable(data.Description_he_il);
        this.Description_en_us = ko.observable(data.Description_en_us);

        this.Price = ko.observable(data.Price);
        this.SupportPrice = ko.observable(data.SupportPrice == undefined ? 0 : data.SupportPrice);
        this.MinimumPrice = ko.observable(data.MinimumPrice == undefined ? 0 : data.MinimumPrice);
        this.TeacherName = ko.observable(data.TeacherName);
        this.StatusValues = ko.observable(data.Status);
        this.SelectedStatus = ko.observable(data.SelectedStatus);

        this.CurrencyId = ko.observable(data.Currency != undefined ? data.Currency.ObjectId : "");
        this.DefaultCurrencyId = ko.observable(data.DefaultCurrencyId);

        this.ComputedCategory2Value = ko.computed(function () {
            var category2Values = self.Category2Values();
            var selectedCat1Value = self.SelectedCategory1Value();
            var result = [];
            $.each(category2Values, function (key, value) {
                if (value.ParentCategoryId === selectedCat1Value) {
                    result.push(value);
                }
            });
            return result;

        }, this);

        this.Price.numericError = ko.observable();
        this.Price.minPriceError = ko.observable();
        this.Price.requiredError = ko.observable();
        this.SupportPrice.numericError = ko.observable();
        this.LessonsExplanation = ko.observable();
        this.BundleName.emptyError = ko.observable();
        this.BundleClips.min2Error = ko.observable();
        this.errorAddingClips = ko.observable();
        this.BundleTitleMask = ko.observable();
        this.firstName = ko.observable(data.FirstName);
        this.lastName = ko.observable(data.LastName);
        this.cityOfResidence = ko.observable(data.CityOfResidence);
        this.validate = function () {
            self.Price.numericError(isNaN(self.Price()));
            self.Price.minPriceError(parseFloat(self.Price()) < parseFloat(self.MinimumPrice()));
            self.Price.requiredError(self.Price() == null || self.Price().length == 0);
            self.SupportPrice.numericError(isNaN(self.SupportPrice()));
            self.BundleClips.min2Error(self.BundleClips().length < 2);

            var hasError =
                self.BundleClips.min2Error() ||
                self.Price.numericError() ||
                    self.SupportPrice.numericError() ||
                    self.Price.minPriceError() ||
                    self.Price.requiredError() ||
                    self.BundleName.emptyError();

            if (hasError) {
                m.hideWait();
            }
            return !hasError;
        };

        this.lessonTitleComputed = ko.computed(function () {
            var template = self.BundleTitleMask();
            if (template === undefined || template ===null) return "";
            var category1 = "[category1]";
            var category2 = "[category2]";
            var category3 = "[category3]";
            var category4 = "[category4]";
            var description = "[description]";
            var remarks = "[remarks]";
            var firstName = "[firstName]";
            var lastName = "[lastName]";
            var cityOfResidence = "[cityOfResidence]";

            var descriptionValue = self.Description_he_il() !== undefined ? self.Description_he_il() : "";
            var remarksValue;
            var title = template;
            var lang = $.cookie("lang") != undefined ? JSON.parse($.cookie("lang")).LanguageCode : "";
            if (lang == "en-US") {
                remarksValue = self.Remarks_en_us() !== undefined ? self.Remarks_en_us() : "";
            } else {
                remarksValue = self.Remarks_he_il() !== undefined ? self.Remarks_he_il() : "";
            }
            var noTokensSelected = self.SelectedCategory1Value() == "" &&
                self.SelectedCategory2Value() == "" &&
                self.SelectedCategory3Value() == "" &&
                self.SelectedCategory4Value() == "" &&
                descriptionValue == "" &&
                remarksValue == "";

            self.BundleName.emptyError(noTokensSelected);

            title = title.replace(category1, self.SelectedCategory1TextComputed());
            title = title.replace(category2, self.SelectedCategory2TextComputed());
            title = title.replace(category3, self.SelectedCategory3TextComputed());
            title = title.replace(category4, self.SelectedCategory4TextComputed());
            title = title.replace(description, descriptionValue);
            title = title.replace(remarks, remarksValue);
            title = title.replace(firstName, self.firstName());
            title = title.replace(lastName, self.lastName());
            title = title.replace(cityOfResidence, self.cityOfResidence());
                        
            while (title.indexOf(",,") != -1) {
                title = title.replace(",,", ",");
                title = title.trim();
            }

            while (title.indexOf(', ,') != -1) {
                title = title.replace(", ,", ",");
                title = title.trim();
            }

            while (title.indexOf(",") == 0) {
                title = title.substring(1);
                title = title.trim();
            }

            while (title.length > 0 && title.lastIndexOf(",") == title.length-1) {
                title = title.substring(0, title.length - 1);
                title = title.trim();
            }

            self.BundleName(title);
            return title;
        }, this);

        this.Save = function () {
            self.ObjectId(self.ObjectIdFixed());
            var model = ko.toJSON(self);
            if (self.validate()) {
            
                new DataModel().setBundleData(model).done(function (e) {
                    if (e.Status == -1) {
                        alert(e.StatusReason);
                        eh.saveBundleClick();
                        m.hideWait();
                    } else {
                        document.location.href = document.location.href;
                    }
                }).fail(function (e) {
                    alert(e.responseText);
                    m.hideWait();
                    });
            } else {
                m.hideWait();
                eh.saveBundleClick();
                vm.isValid(false);
            }
           
        }

        this.saveAsNew = function () {
            self.ObjectId("");
            var model = ko.toJSON(self);
            if (self.validate()) {
                new DataModel().setBundleData(model).done(function(e) {
                    if (e.Status == -1) {
                        alert(e.StatusReason);
                        eh.saveBundleAsNewClick();
                        m.hideWait();
                    } else {
                        document.location.href = document.location.href;
                    }
                }).fail(function(e) {
                    alert(e.responseText);
                    m.hideWait();
                });
            } else {
                m.hideWait();
                eh.saveBundleAsNewClick();
            }
        };

        this.delete = function (bundleId){            
            new DataModel().deleteBundle(bundleId).done(function(e) {
                if (e.status == -1) {
                    alert(e.StatusReason);
                } else {
                    document.location.href = document.location.href;
                }
            }).fail(function (e) {
                alert(e.responseText);
            });
        }
    }

    function searchLessonViewModel(data) {
        if (data == null) data = new Object();
        var self = this;
        this.ObjectId = ko.observable(data.ObjectId);
        this.TeacherId = ko.observable(data.TeacherId);
        this.TeacherName = ko.observable(data.TeacherName);
        this.Category1Values = ko.observableArray(data.Category1Values);
        this.Category2Values = ko.observableArray(data.Category2Values);
        this.Category3Values = ko.observableArray(data.Category3Values);
        this.Category4Values = ko.observableArray(data.Category4Values);

        this.SelectedCategory1Value = ko.observable(data.SelectedCategory1Value);
        this.SelectedCategory2Value = ko.observable(data.SelectedCategory2Value);
        this.SelectedCategory3Value = ko.observable(data.SelectedCategory3Value);
        this.SelectedCategory4Value = ko.observable(data.SelectedCategory4Value);

        this.CategoryName1 = ko.observable(data.CategoryName1);
        this.CategoryName2 = ko.observable(data.CategoryName2);
        this.CategoryName3 = ko.observable(data.CategoryName3);
        this.CategoryName4 = ko.observable(data.CategoryName4);

        this.Category1ValuesForSearch = ko.computed(function () {
            var a = self.Category1Values();
            a.splice(1, 1);
            return a;
        }, this);

        this.Category3ValuesForSearch = ko.computed(function () {
            var a = self.Category3Values();
            a.splice(1, 1);
            return a;
        }, this);

        this.Category4ValuesForSearch = ko.computed(function () {
            var a = self.Category4Values();
            a.splice(1, 1);
            return a;
        }, this);

        this.ComputedCategory2Value = ko.computed(function () {
            var category2Values = self.Category2Values();
            var selectedCat1Value = self.SelectedCategory1Value();
            var result = [];
            $.each(category2Values, function (key, value) {
                if (value.ParentCategoryId === selectedCat1Value) {
                    result.push(value);
                }
            });
            return result;

        }, this);

        this.BundleStatus = ko.observable(data.SelectedStatus);
    }

    var eh = {
        createPackageClick : function() {
            $(".createPackage").click(function (sender) {
                var clipId = sender.currentTarget.dataset.itemId;
                m.showWait(".createPackage");
                new DataModel().getBundleCreateData(clipId).done(function (data) {
                    //data = JSON.parse(data);
                    m.setBundleViewModel(data);
                    eh.saveBundleClick();
                    eh.saveBundleAsNewClick();
                });
            });
        },

        editPackageClick: function () {
            $(".editPackage").click(function(sender) {
                var bundleId = sender.currentTarget.dataset.itemId;
                m.showWait(".editPackage");
                new DataModel().getBundleEditData(bundleId).done(function (data) {
                    m.hideWait(".editPackage");
                    m.setBundleViewModel(data);

                    eh.saveBundleClick();
                    eh.saveBundleAsNewClick();
                });
            });
        },

        chooseLessonsClick: function () {
            $("#chooseLessons").one("click",function(e) {
                var currentTarget = e.currentTarget;
                var allChecked = $(".lessonSearchItem input:checked");
                var existingClips = vm.BundleClips();
                var originalClips = vm.BundleClips().slice();                
                var howManyToRemove = 0;
                vm.BundleClips([]);
                $.each(allChecked, function (key, item) {
                    var clipExists = false;
                    var clipDetails = $(item).data("lesson-details");
                    $.each(existingClips, function(innerKey, innnerItem) {
                        if (clipDetails.Id == innnerItem.Id) {
                            clipExists = true;
                        }
                    });
                    if (!clipExists) {
                        howManyToRemove++;
                        existingClips.push(clipDetails);
                    }
                });
             
                vm.BundleClips(existingClips);
                m.showWait("#chooseLessons");
                new DataModel().getBundleMinimumPrice(existingClips).done(function(data) {
                    if (data.Status == 0) //success
                    {
                        vm.errorAddingClips("");
                        $(currentTarget).closest('.ui-dialog').find('.ui-dialog-titlebar-close').click();
                        vm.MinimumPrice(data.MinimumPrice);
                    } else {
                        m.hideWait("#chooseLessons");
                        eh.chooseLessonsClick();
                        //$(currentTarget).closest('.ui-dialog').find('.ui-dialog-titlebar-close').click();
                        //vm.BundleClips().splice(data.NumberOfPossibleClips-1, vm.BundleClips().length - data.NumberOfPossibleClips);
                        vm.BundleClips(originalClips);
                        vm.errorAddingClips(data.StatusReason);
                        alert(data.StatusReason);
                        vm.MinimumPrice(data.MinimumPrice);
                    }
                    m.hideWait("#chooseLessons");
                });
            });
        },

        deleteLessonClick: function () {
            $(document).on("click", ".deleteLesson", null, function(data) {
                var itemToDelete = $(data.currentTarget).data("item-id");
                var existingClips = vm.BundleClips();
                $.each(existingClips, function (key, item) {
                    var clipId = item !== undefined && item!= null ?item.Id : "";
                    if (clipId === itemToDelete) {
                        vm.BundleClips().splice($.inArray(item, vm.BundleClips()), 1);
                        vm.BundleClips(vm.BundleClips());
                        return;
                    }
                });

                new DataModel().getBundleMinimumPrice(existingClips).done(function (data) {
                    if (data.Status == 0) //success
                    {
                        vm.MinimumPrice(data.MinimumPrice);
                    } 
                });
            });
        },

        addLessonClick : function() {
            $("#findDialog").click(function () {
                $("#dialog_findLesson").dialog("option", "dialogClass", "dialog_searchLesson").dialog("open");
                $("#lessonsSearchResults").html("");

                eh.chooseLessonsClick();
            });
        },

        saveBundleClick: function () {
            $("#saveBundle").off("click");
            $("#saveBundle").one("click", function () {
                m.showWait("#saveBundle");
                vm.Save();
            });
        },

        saveBundleAsNewClick: function () {
            $("#saveBundleAsNew").off("click");
            $("#saveBundleAsNew").one("click", function () {
                m.showWait("#saveBundleAsNew");
                vm.saveAsNew();
            });
        },

        deleteBundleClick: function() {
            $(".deletePackage").on("click", function (sender) {
                var message = $(".deletePackage").data("confirmation-message");
                if (confirm(message)) {
                    m.showWait(".deletePackage");
                    var bundleId = sender.currentTarget.dataset.itemId;
                    vm.delete(bundleId);
                }
            });
        }
    };

    var m= {
        setBundleViewModel : function(data) {
            m.hideWait(".createPackage");
            vm.isValid(true);
            vm.ObjectId(data.ObjectId);
            vm.ObjectIdFixed(data.ObjectId);
            //    vm.BundleName(data.BundleName);                
            vm.BundleClips(data.BundleClips);
            vm.LessonsExplanation(data.LessonsExplanation);

            vm.CategoryName1(data.CategoryName1);
            vm.CategoryName2(data.CategoryName2);
            vm.CategoryName3(data.CategoryName3);
            vm.CategoryName4(data.CategoryName4);

            vm.Category1Values(data.Category1Values);
            vm.Category2Values(data.Category2Values);
            vm.Category3Values(data.Category3Values);
            vm.Category4Values(data.Category4Values);

            vm.BundleTitleMask(data.BundleTitleMask);

            var categoryName1 = { Key: "", Value: data.CategoryName1, AdditionalInfo: {}, LessonType: "", ParentCategoryId: "" };
            var categoryName2 = { Key: "", Value: data.CategoryName2, AdditionalInfo: {}, LessonType: "", ParentCategoryId: "" };
            var categoryName3 = { Key: "", Value: data.CategoryName3, AdditionalInfo: {}, LessonType: "", ParentCategoryId: "" };
            var categoryName4 = { Key: "", Value: data.CategoryName4, AdditionalInfo: {}, LessonType: "", ParentCategoryId: "" };

            data.Category1Values.splice(0, 0, categoryName1);
            data.Category2Values.splice(0, 0, categoryName2);
            data.Category3Values.splice(0, 0, categoryName3);
            data.Category4Values.splice(0, 0, categoryName4);

            vmSearchLessons.Category1Values(data.Category1Values);
            vmSearchLessons.Category2Values(data.Category2Values);
            vmSearchLessons.Category3Values(data.Category3Values);
            vmSearchLessons.Category4Values(data.Category4Values);
            vmSearchLessons.BundleStatus(data.SelectedStatus);
            vmSearchLessons.ObjectId(data.ObjectId);
            vmSearchLessons.TeacherId(data.TeacherId);
            vmSearchLessons.TeacherName(data.TeacherName);

            vm.SelectedCategory1Value(data.SelectedCategory1Value);
            vm.SelectedCategory2Value(data.SelectedCategory2Value);
            vm.SelectedCategory3Value(data.SelectedCategory3Value);
            vm.SelectedCategory4Value(data.SelectedCategory4Value);
            vm.StatusValues(data.StatusValues);
            vm.SelectedStatus(data.SelectedStatus);
            vm.Created(data.Created);
            vm.Updated(data.Updated);
            //    vm.Version(data.Version);

            vm.Remarks_he_il(data.Remarks_he_il);
            vm.Remarks_en_us(data.Remarks_en_us);
            vm.Description_he_il(data.Description_he_il);
            vm.Description_en_us(data.Description_en_us);

            vm.Price(data.Price);
            vm.SupportPrice(data.SupportPrice);
            vm.MinimumPrice(data.MinimumPrice);
            vm.TeacherName(data.TeacherName);
            vm.CurrencyId(data.CurrencyId);
            vm.DefaultCurrencyId(data.DefaultCurrencyId);

            vm.Price.numericError(false);
            vm.Price.minPriceError(false);
            vm.Price.requiredError(false);
            vm.SupportPrice.numericError(false);            
            vm.firstName(data.FirstName);
            vm.lastName(data.LastName);
            vm.cityOfResidence(data.CityOfResidence);

            $("#dialog_editLessonPackage").dialog("option", "dialogClass", "dialog_inputs dialog_editLessonPackage").dialog("open");
        },
        showWait: function () {
            if ($("#domMessage").length > 0) {
                $.blockUI({ message: $("#domMessage") });
            } else {
                $.blockUI();
            }
        },
        hideWait : function() {
            $.unblockUI();
        }
    }

    function updateBundle(name, settings) {
        this.name = name;
        this.settings = settings;
    };

    updateBundle.prototype.init = function () {
        this.base.init.call(this);
    };

    updateBundle.prototype.onLoad = function () {
        this.base.onLoad.call(this);
       
        vm = new bundleUpdateViewModel();
        vmSearchLessons = new searchLessonViewModel();

        ko.applyBindings(vm, $("#dialog_editLessonPackage")[0]);
        ko.applyBindings(vmSearchLessons, $("#dialog_findLesson")[0]);

        eh.createPackageClick();
        eh.editPackageClick();
        eh.deleteLessonClick();
        eh.addLessonClick();
        eh.deleteBundleClick();
    };

    updateBundle.prototype.lessonSearchComplete = function (data) {
        $(".dialog_findLesson_inner .lessonWrapper").click(function () {
            $(this).closest('.lessonSearchItem').find('.cbSearch input').trigger('click');            
        });
        $('body').css('cursor', 'default');
        $(".btn_mentor").css("cursor", "default");
    }

    _mymentor.addModule("updateBundle", updateBundle);
})();
