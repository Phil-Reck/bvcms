﻿$(function () {

    $.fn.popover.Constructor.DEFAULTS.whiteList.table = [];
    $.fn.popover.Constructor.DEFAULTS.whiteList.tr = [];
    $.fn.popover.Constructor.DEFAULTS.whiteList.th = [];
    $.fn.popover.Constructor.DEFAULTS.whiteList.td = [];
    $.fn.popover.Constructor.DEFAULTS.whiteList.div = [];
    $.fn.popover.Constructor.DEFAULTS.whiteList.tbody = [];
    $.fn.popover.Constructor.DEFAULTS.whiteList.thead = [];
    $.fn.popover.Constructor.DEFAULTS.whiteList.button = [];

    var keys = {
        enter: 13,
        tab: 9
    };

    function initializePopovers() {
        $('[data-toggle="popover"]').popover({ html: true });
        $('[data-toggle="popover"]').click(function (ev) {
            ev.preventDefault();
        });
    }
    initializePopovers();

    $('#showcheckimages').change(function () {
        var url = location.pathname + (this.checked ? '?images=1' : '?images=0');
        window.open(url, '_top');
    });

    $('#pid').blur(function () {
        var tr, pid;
        if ($(this).val() === '') {
            return false;
        }
        if ($(this).val() === 'd') {
            tr = $('#bundle > tbody > tr:first');

            var personId = $.trim($("a.pid", tr).text());
            if (personId === 'Select') {
                personId = '';
            }
            pid = personId;

            $('#name').val($("td.name a", tr).text().trim());
            $('#checkno').val($("td.checkno a", tr).text().trim());
            $('#notes').val($("td.notes span", tr).text().trim());
            $('#amt').focus();
            $(this).val($.trim(pid));
            return true;
        }

        var q = $('#pbform').serialize();
        $.post("/PostBundle/GetNamePid/", q, function (ret) {
            if (ret.error === 'not found') {

                $.growl("No Results!", "Person id not found.", "warning");
                $('#name').focus();
                $('#pid').val('');
            }
            else {
                $('#name').val(ret.name);
                $('#pid').val(ret.PeopleId);
                $('#amt').focus();
                showPledgesSummary(ret.pledgesSummary);
            }
        });
    });

    $(".ui-autocomplete-input").on("autocompleteopen", function () {
        var autocomplete = $(this).data("autocomplete"),
            menu = autocomplete.menu;
        if (!autocomplete.options.selectFirst) {
            return;
        }
        menu.activate($.Event({ type: "mouseenter" }), menu.element.children().first());
    });

    $("#name").focus(function () {
        $.enteredname = $(this).val();
    });

    $("#name").autocomplete({
        appendTo: "#SearchResults2",
        autoFocus: true,
        minLength: 1,
        source: function (request, response) {
            if ($("#moreresults").is(":checked")) {
                $.post("/PostBundle/Names2", request, function (ret) {
                    response(ret.slice(0, 30));
                }, "json");
                $("#SearchResults2 > ul").css({
                    'max-height': 400,
                    'overflow-y': 'auto'
                });
            }
            else {
                $.post("/PostBundle/Names", request, function (ret) {
                    response(ret.slice(0, 10));
                }, "json");
            }
        },
        select: function (event, ui) {
            $("#name").val(ui.item.Name);
            $("#pid").val(ui.item.Pid);
            showPledgesSummary(ui.item.pledgesSummary);
            return false;
        }
    }).data("uiAutocomplete")._renderItem = function (ul, item) {
        return $("<li>")
            .append("<a><b>" + item.Name + "</b>" + item.Spouse + item.Email + item.Addr + item.RecentGifts + "</a>")
            .appendTo(ul);
    };

    $("#name").blur(function () {
        if ($('#pid').val() === '' && $(this).val() !== '') {
            $.growl("No Results!", "Name of person not found.", "warning");
            $('#name').val('');
            $('#amt').focus();
        }
        else if ($(this).val() != $.enteredname && $.enteredname != '') {
            var q = $('#pbform').serialize();
            $.post("/PostBundle/GetNamePid/", q, function (ret) {
                if (ret.error === 'not found') {
                    $.growl("No Results!", "Person id not found.", "warning");
                    $('#name').focus();
                    $('#pid').val('');
                }
                else {
                    $('#name').val(ret.name);
                    $('#pid').val(ret.PeopleId);
                    $('#amt').focus();
                }
            });
        }
    });

    var keyallowed = true;

    $('#notes').keypress(function (event) {
        if (keyallowed && event.keyCode === keys.enter && !event.shiftKey) {
            event.preventDefault();
            keyallowed = false;
            $.PostRow({ scroll: false });
        }
    });

    $('#notes').keydown(function (event) {
        if (keyallowed && event.keyCode === keys.tab && !event.shiftKey) {
            event.preventDefault();
            keyallowed = false;
            $.PostRow({ scroll: false });
        }
    });

    $('#pid').keydown(function (event) {
        if (event.keyCode === keys.enter) {
            event.preventDefault();
            $('#name').focus();
        }
    });

    $('#name').keydown(function (event) {
        if (event.keyCode === keys.enter) {
            event.preventDefault();
            $('#amt').focus();
        }
    });

    $('#amt').keydown(function (event) {
        if (event.keyCode === keys.enter) {
            event.preventDefault();
            $('#PLNT').focus();
        }
    });

    $('#PLNT').keydown(function (event) {
        if (event.keyCode === keys.enter) {
            event.preventDefault();
            $('#fund').focus();
        }
    });

    $('#fund').keydown(function (event) {
        if (event.keyCode === keys.enter) {
            event.preventDefault();
            $('#checkno').focus();
        }
    });

    $('#checkno').keydown(function (event) {
        if (event.keyCode === keys.enter) {
            event.preventDefault();
            $('#notes').focus();
        }
    });

    $('a.update').click(function (event) {
        event.preventDefault();
        $('#name').popover('hide');
        $.PostRow({ scroll: true });
    });

    $("body").on("click", 'a.edit', function (ev) {
        ev.preventDefault();
        var tr = $(this).closest("tr");
        $('#editid').val(tr.attr("cid"));

        var personId = $.trim($("a.pid", tr).text());
        if (personId === 'Select') {
            personId = '';
        }

        $('#pid').val(personId);
        $('#name').val($(".name", tr).text().trim());
        $('#contributiondate').val($(".date", tr).val());
        $('#campusid').val($(".campusid", tr).val());
        $("#gear").show();
        $('#fund').val($("td.fund", tr).attr('val'));

        var plnt = $("td.PLNT span", tr).text();
        plnt = plnt || "CN";
        $('#PLNT').val(plnt);

        var a = $('#amt');
        a.val($("td.amt", tr).attr("val"));
        var ckno = $("td.checkno a", tr).text();
        if (ckno === 'Empty')
            ckno = '';
        $('#checkno').val(ckno);
        $('#notes').val($("td.notes span", tr).text());
        tr.hide();
        if (a.val() === '0.00')
            a.val('');
        $('html,body').animate({ scrollTop: 400 }, 600);
        a.focus();
        $('button.contribution-actions').prop('disabled', true);
    });

    $("body").on("click", 'a.split', function (ev) {
        ev.preventDefault();
        $('#split-modal').modal('show');
        $('#contributionId').val($(this).closest("tr").attr('cid'));
    });

    $('#split-modal').on('shown.bs.modal', function () {
        $("#amt-split").val('').focus();
    });

    var postContribution = function (i, amounts) {
        var newamt = parseFloat(amounts[i]);
        if (!isNaN(newamt)) {
            var tr = $('tr[cid=' + $('#contributionId').val() + ']');
            var q = {
                pid: $("a.pid", tr).text(),
                name: $("td.name", tr).text(),
                fund: $("td.fund", tr).attr('val'),
                pledge: $("td.fund", tr).attr('pledge'),
                amt: newamt,
                splitfrom: tr.attr("cid"),
                checkno: $("td.checkno", tr).text(),
                notes: $("td.notes", tr).text(),
                id: $("#id").val()
            };
            $.PostRow({ scroll: true, q: q }).done(function () {
                i++;
                if (i < amounts.length) {
                    postContribution(i, amounts);
                } else {
                    return;
                }
            });
        } else {
            i++;
            if (i < amounts.length) {
                postContribution(i, amounts);
            } else {
                return;
            }
        }
    }

    function splitSubmit() {
        var amounts = $("#amt-split").val().split(' ');
        postContribution(0, amounts);
        $('#split-modal').modal('hide');
    }

    $("#split-submit").click(function (ev) {
        ev.preventDefault();

        splitSubmit();
    });

    $('#amt-split').keydown(function (e) {
        if (e.keyCode === keys.enter) {
            splitSubmit();
        }
    });

    $("body").on("click", 'a.delete', function (ev) {
        ev.preventDefault();
        var tr = $(this).closest("tr");
        $('#editid').val(tr.attr("cid"));
        var q = $('#pbform').serialize();
        $('#editid').val('');
        swal({
            title: "Are you sure?",
            type: "warning",
            showCancelButton: true,
            confirmButtonClass: "btn-danger",
            confirmButtonText: "Yes, delete it!",
            closeOnConfirm: true
        },
            function () {
                $.post("/PostBundle/DeleteRow/", q, function (ret) {
                    if (ret && ret.error)
                        $.growl("Error!", ret.error, "danger");
                    else {
                        tr.remove();
                        showHideDiffRow(ret.diff);
                        $('.totalitems').text(ret.totalitems);
                        $('.difference').text(ret.difference);
                        $('.itemcount').text(ret.itemcount);
                        $('#editid').val('');
                        $.growl("Deleted!", "Contribution was successfully deleted.", "success");
                    }
                });
            });
    });

    function showHideDiffRow(diff) {
        if (diff === 0) {
            $('tr.diffRow').hide();
        } else {
            $('tr.diffRow').show();
        }
    }

    $.fn.editableform.buttons = '<button type="submit" class="btn btn-primary btn-sm editable-submit">' +
        '<i class="fa fa-fw fa-check"></i>' +
        '</button>' +
        '<button type="button" class="btn btn-default btn-sm editable-cancel">' +
        '<i class="fa fa-fw fa-times"></i>' +
        '</button>';
    function initializeEditable() {
        $(".clickEdit").editable({
            mode: 'popup',
            type: 'text',
            url: "/PostBundle/Edit/",
            params: function (params) {
                var data = {};
                data['id'] = params.pk;
                data['value'] = params.value;
                return data;
            },
            success: function (ret) {
                showHideDiffRow(ret.diff);
                $('.totalitems').text(ret.totalitems);
                $('.difference').text(ret.difference);
                $('.itemcount').text(ret.itemcount);
                $('#a' + ret.cid).closest('td').attr('val', ret.amt);
            }
        });

        $(".clickSelect").editable({
            mode: 'popup',
            type: 'select',
            url: "/PostBundle/Edit/",
            source: "/PostBundle/Funds/",
            params: function (params) {
                var data = {};
                data['id'] = params.pk;
                data['value'] = params.value;
                return data;
            },
            success: function (ret, value) {
                $(this).closest('td').attr('val', value);
            }
        });
    }
    initializeEditable();

    $.PostRow = function (options) {
        var dfrd = new $.Deferred();
        $('#name').popover('hide');
        if (!options.q) {
            var n = parseFloat($('#amt').val());
            var plnt = $("#PLNT").val();
            if (!n > 0 && plnt != 'GK' && plnt != 'SK') {
                $.growl("Contribution Error!", "Cannot post. No amount specified.", "danger");
                keyallowed = true;
                return true;
            }
            if (!isNaN(n) && n != 0 && plnt === 'GK') {
                $.growl("Contribution Error!", "Cannot post. Gift In Kind must be zero.", "danger");
                keyallowed = true;
                return true;
            }
            options.q = $('#pbform').serialize();
        }
        var action = "/PostBundle/PostRow/";
        var cid = $('#editid').val();
        if (cid) {
            action = "/PostBundle/UpdateRow/";
        }
        $.post(action, options.q).done(function (ret) {
            keyallowed = true;
            if (!ret)
                return;
            if (ret.error) {
                $.growl("Error!", ret.error, "danger");
                return;
            }
            showHideDiffRow(ret.diff);
            $('.totalitems').text(ret.totalitems);
            $('.difference').text(ret.difference);
            $('.itemcount').text(ret.itemcount);
            var pid = $('#pid').val();
            var tr;
            if (cid) {
                tr = $('tr[cid="' + cid + '"]');
                tr.replaceWith(ret.row);
                tr = $('tr[cid="' + cid + '"]');
            }
            else if (options.q.splitfrom) {
                tr = $('tr[cid="' + options.q.splitfrom + '"]');
                $('#a' + options.q.splitfrom).text(ret.othersplitamt);
                $(ret.row).insertAfter(tr);
                tr = $('tr[cid="' + ret.cid + '"]');
            }
            else {
                $('#bundle tbody').prepend(ret.row);
                tr = $('#bundle tbody tr:first');
            }
            $('td.name', tr).tooltip({ showBody: "|" });
            $('button.contribution-actions').prop('disabled', false);
            $('#editid').val('');
            $('#entry input').val('');
            $('#fund').val($('#fundid').val());
            $('#pid').focus();

            var top = tr.offset().top - 360;
            if (options.scroll) {
                $('html,body').animate({ scrollTop: top }, 1000);
            }
            $(tr).children('td').effect("highlight", { color: '#eaab00' }, 3000);
            $("#gear").hide();
            initializePopovers();
            initializeEditable();
            dfrd.resolve();
        });
        return dfrd.promise();
    };


    $("#showmove").click(function (ev) {
        ev.preventDefault();
        $('#move-modal').modal('show');
    });

    $('#move-modal').on('shown.bs.modal', function () {
        $("#moveto").val('').focus();
    });

    $("#moveit").click(function (ev) {
        ev.preventDefault();
        $.post("/PostBundle/Move/" + $("#editid").val(), { moveto: $("#moveto").val() }, function (ret) {
            $('#move-modal').modal('hide');
            if (ret.status === "ok") {
                $('#editid').val('');
                $('#entry input').val('');
                $('#fund').val($('#fundid').val());
                $('#pid').focus();
                showHideDiffRow(ret.diff);
                $('.totalitems').text(ret.totalitems);
                $('.difference').text(ret.difference);
                $('.itemcount').text(ret.itemcount);
                keyallowed = true;
                $.growl("Moved!", "Contribution was successfully moved.", "success");
                $('button.contribution-actions').prop('disabled', false);
                $("#gear").hide();
            } else {
                $.growl("Error!", ret.error, "danger");
            }
        });
    });

    $("#movecancel").click(function (ev) {
        ev.preventDefault();
        $.unblockUI();
    });

    $("#showdate").click(function (ev) {
        ev.preventDefault();
        $('#edit-date-modal').modal('show');
    });

    $('#edit-date-modal').on('shown.bs.modal', function () {
        $("#newcontributiondate").val($("#contributiondate").val()).focus();
        $.InitializeDateElements();
    });

    $("#editdatedone").click(function (ev) {
        ev.preventDefault();
        $("#contributiondate").val($("#newcontributiondate").val());
        $('#edit-date-modal').modal('hide');
    });

    $("#showcampus").click(function (ev) {
        ev.preventDefault();
        $('#edit-campus-modal').modal('show');
    });

    $('#edit-campus-modal').on('shown.bs.modal', function () {
        $("#newcampus").val($("#campusid").val()).focus();
        $.InitializeDateElements();
    });

    $("#editcampusdone").click(function (ev) {
        ev.preventDefault();
        $("#campusid").val($("#newcampus").val());
        $('#edit-campus-modal').modal('hide');
    });

    $('#name').popover({
        title: 'Pledges Summary <span class="close">&times;</span>',
        content: 'Loading...',
        trigger: 'manual',
        placement: 'top',
        html: true
    }).on('shown.bs.popover', function () {
        var popover = $(this);
        $(this).parent().find('div.popover .close').on('click', function () {
            popover.popover('hide');
        });
    });
});

function setPledges(pledgesData) {
    var pledges = '<div><table><thead><tr><th id="pledgesTable">Id</th><th id="pledgesTable">Fund</th><th id="pledgesTable"">Pledged</th><th id="pledgesTable"">Contributed</th><th id="pledgesTable">Balance</th></tr></thead>';
    $.each(pledgesData, function (idx, obj) {
        pledges = pledges + '<tr><td id="pledgesTable">' + obj.FundId + '</td><td id="pledgesTable">' + obj.FundName + '</td><td id="pledgesTable">' + obj.AmountPledged + '</td><td id="pledgesTable">' + obj.AmountContributed + '</td><td id="pledgesTable">' + obj.Balance + '</td></tr>';
    });
    pledges = pledges + '</table></div>';
    $('#name').attr('data-content', pledges);
    var popoverPledges = $('#name').data('bs.popover');
    popoverPledges.setContent();
    popoverPledges.$tip.addClass(popoverPledges.options.placement);
    popoverPledges.$tip.css('max-width', '100%');
}

function showPledgesSummary(pledgesData) {
    if (Array.isArray(pledgesData) && pledgesData.length) {
        setPledges(pledgesData);
        $('#name').popover('show');
    }
}

function AddSelected(ret) {
    var tr = $('tr[cid=' + ret.cid + ']');
    $('.pid', tr).text(ret.pid);
    $('.name', tr).text(ret.name);

    $('.edit', tr).click();
}
