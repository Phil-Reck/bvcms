﻿<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl<CmsWeb.Models.OnlineRegPersonModel2>" %>
<%=Html.Hidden3("m.List[" + Model.index + "].first", Model.first) %>
<%=Html.Hidden3("m.List[" + Model.index + "].middle", Model.middle) %>
<%=Html.Hidden3("m.List[" + Model.index + "].last", Model.last) %>
<%=Html.Hidden3("m.List[" + Model.index + "].suffix", Model.suffix) %>
<%=Html.Hidden3("m.List[" + Model.index + "].dob", Model.dob) %>
<%=Html.Hidden3("m.List[" + Model.index + "].phone", Model.phone) %>
<%=Html.Hidden3("m.List[" + Model.index + "].address", Model.address) %>
<%=Html.Hidden3("m.List[" + Model.index + "].email", Model.email) %>
<%=Html.Hidden3("m.List[" + Model.index + "].zip", Model.zip) %>
<%=Html.Hidden3("m.List[" + Model.index + "].city", Model.city) %>
<%=Html.Hidden3("m.List[" + Model.index + "].state", Model.state) %>
<%=Html.Hidden3("m.List[" + Model.index + "].gender", Model.gender) %>
<%=Html.Hidden3("m.List[" + Model.index + "].married", Model.married) %>
<%=Html.Hidden3("m.List[" + Model.index + "].whatfamily", Model.whatfamily) %>