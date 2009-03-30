using System;
using System.Collections.Specialized;
using System.Web.Mvc;
using Suteki.Common.Repositories;
using Suteki.Common.Validation;
using Suteki.Shop.Services;

namespace Suteki.Shop.Binders
{
	public class OrderBinder : IModelBinder
	{
		readonly IValidatingBinder validatingBinder;
		readonly IRepository<Country> countryRepository;
		readonly IEncryptionService encryptionService;

		public OrderBinder(IValidatingBinder validatingBinder, IRepository<Country> countryRepository,
		                   IEncryptionService encryptionService)
		{
			this.validatingBinder = validatingBinder;
			this.encryptionService = encryptionService;
			this.countryRepository = countryRepository;
		}

		public object BindModel(ControllerContext controllerContext, ModelBindingContext bindingContext)
		{
			var form = controllerContext.HttpContext.Request.Form;

			var order = new Order
			{
				OrderStatusId = OrderStatus.CreatedId,
				CreatedDate = DateTime.Now,
				DispatchedDate = DateTime.Now
			};

			try
			{
				var validator = new Validator
				{
					() => UpdateOrder(order, form),
					() => UpdateCardContact(order, form, bindingContext.ModelState),
					() => UpdateDeliveryContact(order, form, bindingContext.ModelState),
					() => UpdateCard(order, form)
				};

				validator.Validate();
			}
			catch (ValidationException exception)
			{
				//Ignore validation exceptions - they will be stored in ModelState.
			}
			return order;
		}

		void UpdateCardContact(Order order, NameValueCollection form, ModelStateDictionary modelState)
		{
			var cardContact = new Contact();
			order.Contact = cardContact;
			UpdateContact(cardContact, "cardcontact", form, modelState);
		}

		void UpdateDeliveryContact(Order order, NameValueCollection form, ModelStateDictionary modelState)
		{
			if (order.UseCardHolderContact) return;

			var deliveryContact = new Contact();
			order.Contact1 = deliveryContact;
			UpdateContact(deliveryContact, "deliverycontact", form, modelState);
		}

		void UpdateContact(Contact contact, string prefix, NameValueCollection form, ModelStateDictionary modelState)
		{
			try
			{
				validatingBinder.UpdateFrom(contact, form, modelState, prefix);
			}
			finally
			{
				if (contact.CountryId != 0 && contact.Country == null)
				{
					contact.Country = countryRepository.GetById(contact.CountryId);
				}
			}
		}

		void UpdateCard(Order order, NameValueCollection form)
		{
			if (order.PayByTelephone) return;

			var card = new Card();
			order.Card = card;
			validatingBinder.UpdateFrom(card, form, "card");
			encryptionService.EncryptCard(card);
		}

		void UpdateOrder(Order order, NameValueCollection form)
		{
			validatingBinder.UpdateFrom(order, form, "order");
			var confirmEmail = form["emailconfirm"];
			if (order.Email != confirmEmail)
			{
				throw new ValidationException("Email and Confirm Email do not match");
			}
		}
	}
}