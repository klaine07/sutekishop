using System;
using MvcContrib;
using System.Collections.Specialized;
using System.Web.Mvc;
using Suteki.Common.Binders;
using Suteki.Common.Extensions;
using Suteki.Common.Filters;
using Suteki.Common.Repositories;
using Suteki.Common.Services;
using Suteki.Common.Validation;
using Suteki.Shop.Binders;
using Suteki.Shop.Repositories;
using Suteki.Shop.Services;
using Suteki.Shop.ViewData;

namespace Suteki.Shop.Controllers
{
	public class CheckoutController : ControllerBase
	{
		readonly IRepository<Basket> basketRepository;
		readonly IUserService userService;
		readonly IPostageService postageService;
		readonly IRepository<Country> countryRepository;
		readonly IRepository<CardType> cardTypeRepository;
		readonly IRepository<Order> orderRepository;
		readonly IValidatingBinder validatingBinder;
		readonly IEmailSender emailSender;
		readonly IEncryptionService encryptionService;
		readonly IUnitOfWorkManager unitOfWork;

		public CheckoutController(IRepository<Basket> basketRepository, IUserService userService,
		                          IPostageService postageService, IRepository<Country> countryRepository,
		                          IRepository<CardType> cardTypeRepository, IRepository<Order> orderRepository,
		                          IValidatingBinder validatingBinder, IEmailSender emailSender,
		                          IEncryptionService encryptionService, IUnitOfWorkManager unitOfWork)
		{
			this.basketRepository = basketRepository;
			this.unitOfWork = unitOfWork;
			this.encryptionService = encryptionService;
			this.emailSender = emailSender;
			this.validatingBinder = validatingBinder;
			this.orderRepository = orderRepository;
			this.cardTypeRepository = cardTypeRepository;
			this.countryRepository = countryRepository;
			this.postageService = postageService;
			this.userService = userService;
		}

		public ActionResult Index(int id)
		{
			// create a default order
			var order = new Order {UseCardHolderContact = true};

			var basket = basketRepository.GetById(id);
			PopulateOrderForView(order, basket);

			return View(CheckoutViewData(order));
		}

		static void PopulateOrderForView(Order order, Basket basket)
		{
			if (order.Basket == null) order.Basket = basket;
			if (order.Contact == null) order.Contact = new Contact();
			if (order.Contact1 == null) order.Contact1 = new Contact();
			if (order.Card == null) order.Card = new Card();
		}

		ShopViewData CheckoutViewData(Order order)
		{
			userService.CurrentUser.EnsureCanViewOrder(order);
			postageService.CalculatePostageFor(order);

			return ShopView.Data
				.WithCountries(countryRepository.GetAll().Active().InOrder())
				.WithCardTypes(cardTypeRepository.GetAll())
				.WithOrder(order);
		}

		[AcceptVerbs(HttpVerbs.Post), UnitOfWork]
		public ActionResult PlaceOrder([BindUsing(typeof(OrderBinder))] Order order)
		{

			if (ModelState.IsValid)
			{
				orderRepository.InsertOnSubmit(order);
				userService.CurrentUser.CreateNewBasket();

				//we need an explicit Commit in order to obtain the db-generated Order Id
				unitOfWork.Commit();

				EmailOrder(order);
				
				
				return this.RedirectToAction<OrderController>(c => c.Item(order.OrderId));
			}

			return this.RedirectToAction(x => x.Index(order.BasketId));

			//try
			//{
				//UpdateOrderFromForm(order, form);
//				orderRepository.InsertOnSubmit(order);
//				userService.CurrentUser.CreateNewBasket();
//				orderRepository.SubmitChanges();
//				EmailOrder(order);

//				return RedirectToRoute(new {Controller = "Order", Action = "Item", id = order.OrderId});
			//}
//			catch (ValidationException validationException)
//			{
//				var basket = basketRepository.GetById(order.BasketId);
//				PopulateOrderForView(order, basket);
//				return View("Index", CheckoutViewData(order).WithErrorMessage(validationException.Message));
//			}
		}

	/*	void UpdateOrderFromForm(Order order, NameValueCollection form)
		{
			order.OrderStatusId = OrderStatus.CreatedId;
			order.CreatedDate = DateTime.Now;
			order.DispatchedDate = DateTime.Now;

			var validator = new Validator
			{
				() => UpdateOrder(order, form),
				() => UpdateCardContact(order, form),
				() => UpdateDeliveryContact(order, form),
				() => UpdateCard(order, form)
			};

			validator.Validate();
		}*/


		
		

		[NonAction]
		public virtual void EmailOrder(Order order)
		{
			//TODO: This needs cleaning up. 

			var result = View("~/Views/Order/Print.aspx", "Print", CheckoutViewData(order));

			var subject = "{0}: your order".With(BaseControllerService.ShopName);
			var message = this.CaptureActionHtml(c => result);
			var toAddresses = new[] { order.Email, BaseControllerService.EmailAddress };

			// send the message
			emailSender.Send(toAddresses, subject, message);
		}

		public ActionResult UpdateCountry(int id, int countryId, FormCollection form)
		{
/*
			var basket = basketRepository.GetById(id);
			basket.CountryId = countryId;
			basketRepository.SubmitChanges();

			var order = new Order();

			try
			{
				UpdateOrderFromForm(order, form);
			}
			catch (ValidationException)
			{
				// ignore validation exceptions
			}

			PopulateOrderForView(order, basket);
			return View("Checkout", CheckoutViewData(order));
*/
			throw new NotImplementedException();
		}
	}
}