﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Discounts;
using Nop.Plugin.DiscountRules.HasOneProduct.Models;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Security;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Models.Catalog;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.DiscountRules.HasOneProduct.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    public class DiscountRulesHasOneProductController : BasePluginController
    {
        #region Fields

        private readonly IDiscountService _discountService;
        private readonly IPermissionService _permissionService;
        private readonly IProductModelFactory _productModelFactory;
        private readonly IProductService _productService;
        private readonly ISettingService _settingService;

        #endregion

        #region Ctor

        public DiscountRulesHasOneProductController(IDiscountService discountService,
            IPermissionService permissionService,
            IProductModelFactory productModelFactory,
            IProductService productService,
            ISettingService settingService)
        {
            _discountService = discountService;
            _permissionService = permissionService;
            _productModelFactory = productModelFactory;
            _productService = productService;
            _settingService = settingService;
        }

        #endregion

        #region Methods

        public IActionResult Configure(int discountId, int? discountRequirementId)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageDiscounts))
                return Content("Access denied");

            //load the discount
            var discount = _discountService.GetDiscountById(discountId);
            if (discount == null)
                throw new ArgumentException("Discount could not be loaded");

            //check whether the discount requirement exists
            if (discountRequirementId.HasValue && !discount.DiscountRequirements.Any(requirement => requirement.Id == discountRequirementId.Value))
                return Content("Failed to load requirement.");

            //try to get previously saved restricted product identifiers
            var restrictedProductIds = _settingService.GetSettingByKey<string>(string.Format(DiscountRequirementDefaults.SettingsKey, discountRequirementId ?? 0));

            var model = new RequirementModel
            {
                RequirementId = discountRequirementId ?? 0,
                DiscountId = discountId,
                Products = restrictedProductIds
            };

            //set the HTML field prefix
            ViewData.TemplateInfo.HtmlFieldPrefix = string.Format(DiscountRequirementDefaults.HtmlFieldPrefix, discountRequirementId ?? 0);

            return View("~/Plugins/DiscountRules.HasOneProduct/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAntiForgery]
        public IActionResult Configure(int discountId, int? discountRequirementId, string productIds)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageDiscounts))
                return Content("Access denied");

            //load the discount
            var discount = _discountService.GetDiscountById(discountId);
            if (discount == null)
                throw new ArgumentException("Discount could not be loaded");

            //get the discount requirement
            var discountRequirement = discountRequirementId.HasValue
                ? discount.DiscountRequirements.FirstOrDefault(requirement => requirement.Id == discountRequirementId.Value) : null;

            //the discount requirement does not exist, so create a new one
            if (discountRequirement == null)
            {
                discountRequirement = new DiscountRequirement
                {
                    DiscountRequirementRuleSystemName = DiscountRequirementDefaults.SystemName
                };
                discount.DiscountRequirements.Add(discountRequirement);
                _discountService.UpdateDiscount(discount);
            }

            //save restricted product identifiers
            _settingService.SetSetting(string.Format(DiscountRequirementDefaults.SettingsKey, discountRequirement.Id), productIds);

            return Json(new { Result = true, NewRequirementId = discountRequirement.Id });
        }

        public IActionResult ProductAddPopup()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageProducts))
                return AccessDeniedView();

            //prepare model
            var model = _productModelFactory.PrepareProductSearchModel(new ProductSearchModel());

            return View("~/Plugins/DiscountRules.HasOneProduct/Views/ProductAddPopup.cshtml", model);
        }

        [HttpPost]
        [AdminAntiForgery]
        public IActionResult LoadProductFriendlyNames(string productIds)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManageProducts))
                return Json(new { Text = string.Empty });

            if (string.IsNullOrWhiteSpace(productIds))
                return Json(new { Text = string.Empty });

            var result = string.Empty;
            var ids = new List<int>();
            var rangeArray = productIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();

            //we support three ways of specifying products:
            //1. The comma-separated list of product identifiers (e.g. 77, 123, 156).
            //2. The comma-separated list of product identifiers with quantities.
            //      {Product ID}:{Quantity}. For example, 77:1, 123:2, 156:3
            //3. The comma-separated list of product identifiers with quantity range.
            //      {Product ID}:{Min quantity}-{Max quantity}. For example, 77:1-3, 123:2-5, 156:3-8
            foreach (var str1 in rangeArray)
            {
                var str2 = str1;
                //we do not display specified quantities and ranges
                //so let's parse only product names (before : sign)
                if (str2.Contains(":"))
                    str2 = str2.Substring(0, str2.IndexOf(":"));

                if (int.TryParse(str2, out var tmp1))
                    ids.Add(tmp1);
            }

            var products = _productService.GetProductsByIds(ids.ToArray());
            for (var i = 0; i <= products.Count - 1; i++)
            {
                result += products[i].Name;
                if (i != products.Count - 1)
                    result += ", ";
            }

            return Json(new { Text = result });
        }

        #endregion
    }
}