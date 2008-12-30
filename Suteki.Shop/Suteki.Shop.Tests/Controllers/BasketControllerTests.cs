﻿using System.Web.Mvc;
using NUnit.Framework;
using Rhino.Mocks;
using Suteki.Common.Repositories;
using Suteki.Common.Validation;
using Suteki.Shop.Controllers;
using Suteki.Shop.Services;
using Suteki.Shop.Tests.TestHelpers;
using Suteki.Shop.ViewData;

namespace Suteki.Shop.Tests.Controllers
{
    [TestFixture]
    public class BasketControllerTests
    {
        #region Setup/Teardown

        [SetUp]
        public void SetUp()
        {
            basketRepository = MockRepository.GenerateStub<IRepository<Basket>>();
            basketRepository = MockRepository.GenerateStub<IRepository<Basket>>();
            basketItemRepository = MockRepository.GenerateStub<IRepository<BasketItem>>();
            sizeRepository = MockRepository.GenerateStub<IRepository<Size>>();

            userService = MockRepository.GenerateStub<IUserService>();
            postageService = MockRepository.GenerateStub<IPostageService>();
            countryRepository = MockRepository.GenerateStub<IRepository<Country>>();

            validatingBinder = new ValidatingBinder(new SimplePropertyBinder());

            basketController = new BasketController(
                basketRepository,
                basketItemRepository,
                sizeRepository,
                userService,
                postageService,
                countryRepository,
                validatingBinder);

            testContext = new ControllerTestContext(basketController);
        }

        #endregion

        private BasketController basketController;
        private ControllerTestContext testContext;

        private IRepository<Basket> basketRepository;
        private IRepository<BasketItem> basketItemRepository;
        private IRepository<Size> sizeRepository;

        private IUserService userService;
        private IPostageService postageService;
        private IRepository<Country> countryRepository;
        private IValidatingBinder validatingBinder;

        private User CreateUserWithBasket()
        {
            var user = new User
            {
                RoleId = Role.GuestId,
                Baskets =
                    {
                        new Basket()
                    }
            };
            userService.Expect(bc => bc.CurrentUser).Return(user);
            return user;
        }

        private static FormCollection CreateUpdateForm()
        {
            var form = new FormCollection
            {
                {"sizeid", "5"},
                {"quantity", "2"}
            };
            return form;
        }

        [Test]
        public void Index_ShouldShowIndexViewWithCurrentBasket()
        {
            var user = CreateUserWithBasket();
            testContext.TestContext.Context.User = user;

            var result = basketController.Index() as ViewResult;

            Assert.AreEqual("Index", result.ViewName);
            var viewData = result.ViewData.Model as ShopViewData;
            Assert.IsNotNull(viewData, "viewData is not ShopViewData");

            Assert.AreSame(user.Baskets[0], viewData.Basket, "The user's basket has not been shown");
        }

        [Test]
        public void Remove_ShouldRemoveItemFromBasket()
        {
            const int basketItemIdToRemove = 3;

            var user = CreateUserWithBasket();
            var basketItem = new BasketItem
            {
                BasketItemId = basketItemIdToRemove,
                Quantity = 1,
                Size = new Size
                {
                    Product = new Product {Weight = 100}
                }
            };
            user.Baskets[0].BasketItems.Add(basketItem);
            testContext.TestContext.Context.User = user;

            // expect 
            basketItemRepository.Expect(ir => ir.DeleteOnSubmit(basketItem));
            basketItemRepository.Expect(ir => ir.SubmitChanges());

            var result = basketController.Remove(basketItemIdToRemove) as ViewResult;

            Assert.AreEqual("Index", result.ViewName);
            basketItemRepository.VerifyAllExpectations();
        }

        [Test]
        public void Update_ShouldAddBasketLineToCurrentBasket()
        {
            var form = CreateUpdateForm();
            var user = CreateUserWithBasket();

            // expect 
            basketRepository.Expect(or => or.SubmitChanges());
            userService.Expect(us => us.CreateNewCustomer()).Return(user);
            userService.Expect(bc => bc.SetAuthenticationCookie(user.Email));
            userService.Expect(bc => bc.SetContextUserTo(user));

            var size = new Size
            {
                IsInStock = true,
                Product = new Product
                {
                    Weight = 10
                }
            };
            sizeRepository.Expect(sr => sr.GetById(5)).Return(size);

            basketController.Update(form);

            Assert.AreEqual(1, user.Baskets[0].BasketItems.Count, "expected BasketItem is missing");
            Assert.AreEqual(5, user.Baskets[0].BasketItems[0].SizeId);
            Assert.AreEqual(2, user.Baskets[0].BasketItems[0].Quantity);

            basketRepository.VerifyAllExpectations();
            userService.VerifyAllExpectations();
        }

        [Test]
        public void Update_ShouldShowErrorMessageIfItemIsOutOfStock()
        {
            var form = CreateUpdateForm();
            var user = CreateUserWithBasket();

            // expect 
            basketRepository.Expect(or => or.SubmitChanges());
            userService.Expect(us => us.CreateNewCustomer()).Return(user);
            userService.Expect(bc => bc.SetAuthenticationCookie(user.Email));
            userService.Expect(bc => bc.SetContextUserTo(user));

            var size = new Size
            {
                Name = "S",
                IsInStock = false,
                IsActive = true,
                Product = new Product {Name = "Denim Jacket"}
            };
            sizeRepository.Expect(sr => sr.GetById(5)).Return(size);

            const string expectedMessage = "Sorry, Denim Jacket, Size S is out of stock.";

            basketController.Update(form)
                .ReturnsViewResult()
                .ForView("Index")
                .AssertAreEqual<ShopViewData, string>(expectedMessage, vd => vd.ErrorMessage);

            Assert.AreEqual(0, user.Baskets[0].BasketItems.Count, "should not be any basket items");
        }
    }
}