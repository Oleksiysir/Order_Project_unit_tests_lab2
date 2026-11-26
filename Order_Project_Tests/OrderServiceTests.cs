using Microsoft.Extensions.DependencyInjection;
using Moq;
using Order_Project.Models;
using Order_Project.Services;
using Order_Project.Services.Intefraces;

namespace Order_Project_Tests
{
    public class OrderServiceTests
    {
        private const string _laptop = "Laptop";
        private const string _phone = "Phone";
        private const string _keyboard = "Keyboard";
        private const int _quantity = 2;
        private const int _updatedQuantity = 3;
        private const int _negativeQuantity = -1;
        private const int _zeroQuantity = 0;
        private const int _missingOrderId = 999;
        private readonly Mock<IInventoryService> _inventoryMock;
        private readonly Mock<IPaymentService> _paymentMock;
        private readonly Mock<INotificationService> _notificationMock;
        private readonly OrderService _service;

        public OrderServiceTests()
        {
            _inventoryMock = new Mock<IInventoryService>();
            _paymentMock = new Mock<IPaymentService>();
            _notificationMock = new Mock<INotificationService>();

            _service = new OrderService(_inventoryMock.Object, _paymentMock.Object, _notificationMock.Object);
        }

        /// <summary>
        /// Helper method configures the inventory and payment services
        /// to simulate successful order processing.
        /// </summary>
        /// <param name="product"></param>
        /// <param name="quantity"></param>
        private void SetupSuccessfulDependencies(string product, int quantity)
        {
            _inventoryMock.Setup(x => x.CheckStock(product, quantity)).Returns(true);

            _paymentMock.Setup(x => x.ProcessPayment(It.IsAny<Order>())).Returns(true);
        }


        /// <summary>
        /// Verifies that an order can be created with different product names.
        /// The created order should contain the expected product name.
        /// </summary>
        [Theory]
        [InlineData(_laptop)]
        [InlineData(_phone)]
        [InlineData(_keyboard)]
        public void CreateOrder_ValidProduct_ReturnsExpectedProduct(string product)
        {
            //arrange
            SetupSuccessfulDependencies(product, _quantity);

            //act
            var actual = _service.CreateOrder(product, _quantity);
            
            //assert
            Assert.Equal(product, actual.Product);
        }

        /// <summary>
        /// Verifies that two independently created orders receive different IDs.
        /// Each order should have a unique identifier.
        /// </summary>
        [Fact]
        public void CreateOrder_TwoOrders_AssignsDifferentIds()
        {
            //arrange
            _inventoryMock.Setup(x => x.CheckStock(_laptop, _quantity)).Returns(true);

            _inventoryMock.Setup(x => x.CheckStock(_phone, _quantity)).Returns(true);

            _paymentMock.Setup(x => x.ProcessPayment(It.IsAny<Order>())).Returns(true);

            //act
            var firstOrder = _service.CreateOrder(_laptop, _quantity);
            var secondOrder = _service.CreateOrder(_phone, _quantity);

            //assert
            Assert.NotEqual(firstOrder.Id, secondOrder.Id);
        }

        /// <summary>
        /// Verifies that an existing order can be successfully updated.
        /// The UpdateOrder method should return true.
        /// </summary>
        [Fact]
        public void UpdateOrder_ExistingOrder_ReturnsTrue()
        {
            //arrange
            SetupSuccessfulDependencies(_laptop, _quantity);
            var order = _service.CreateOrder(_laptop, _quantity);

            //act
            var actual = _service.UpdateOrder(order.Id, _updatedQuantity);

            //assert
            Assert.True(actual);
        }

        /// <summary>
        /// Verifies that the quantity of an existing order is updated correctly.
        /// The order should contain the new quantity after the update.
        /// </summary>
        [Fact]
        public void UpdateOrder_ExistingOrder_ChangesQuantity()
        {
            //arrange
            SetupSuccessfulDependencies(_laptop, _quantity);
            var order = _service.CreateOrder(_laptop, _quantity);

            //act
            _service.UpdateOrder(order.Id, _updatedQuantity);

            //assert
            Assert.Equal(_updatedQuantity, order.Quantity);
        }

        /// <summary>
        /// Verifies that an attempt to update a non-existing order fails.
        /// The UpdateOrder method should return false.
        /// </summary>
        [Fact]
        public void UpdateOrder_MissingOrder_ReturnsFalse()
        {
            //arrange
            //no orders have been created

            //act
            var actual = _service.UpdateOrder(_missingOrderId, _quantity);

            //assert
            Assert.False(actual);
        }

        /// <summary>
        /// Verifies that FirstOrDefault returns null when the order collection is empty.
        /// </summary>
        [Fact]
        public void GetOrders_EmptyCollection_FirstOrDefaultReturnsNull()
        {
            //arrange
            //the order collections is empty

            //act
            var actual = _service.GetOrders().FirstOrDefault();

            //assert
            Assert.Null(actual);
        }

        /// <summary>
        /// Verifies that a newly created order is present in the order collection.
        /// The collection should contain the created order.
        /// </summary>
        [Fact]
        public void GetOrders_AfterCreation_ContainsCreatedOrder()
        {
            //arrange
            SetupSuccessfulDependencies(_laptop,_quantity);
            var order = _service.CreateOrder(_laptop, _quantity);

            //act
            var actual = _service.GetOrders();

            //assert
            Assert.Contains(order, actual);
        }

        /// <summary>
        /// Verifies that order creation fails when payment processing is unsuccessful.
        /// An InvalidOperationException should be thrown.
        /// </summary>
        [Fact]
        public void CreateOrder_PaymentFails_ThrowsInvalidOperationException()
        {
            //arrange
            _inventoryMock.Setup(x => x.CheckStock(_laptop, _quantity)).Returns(true);

            _paymentMock.Setup(x => x.ProcessPayment(It.IsAny<Order>())).Returns(false);

            Action act = () => _service.CreateOrder(_laptop, _quantity);

            //act and assert
            Assert.Throws<InvalidOperationException>(act);
        }

        /// <summary>
        /// Verifies that an order is not saved when payment processing fails.
        /// The order collection should remain empty.
        /// </summary>
        [Fact]
        public void CreateOrder_PaymentFails_LeavesCollectionEmpty()
        {
            //arrange
            _inventoryMock.Setup(x => x.CheckStock(_laptop, _quantity)).Returns(true);
            _paymentMock.Setup(x => x.ProcessPayment(It.IsAny<Order>())).Returns(false);

            //act
            Record.Exception(() => _service.CreateOrder(_laptop, _quantity));
            var actual = _service.GetOrders();

            //assert
            Assert.Empty(actual);
        }

        /// <summary>
        /// Verifies that creating an order with insufficient stock throws an exception.
        /// An InvalidOperationException should be thrown.
        /// </summary>
        [Fact]
        public void CreateOrder_InsufficientStock_ThrowsInvalidOperationException()
        {
            //arrange
            _inventoryMock.Setup(x => x.CheckStock(_laptop, _quantity)).Returns(false);

            Action act = () => _service.CreateOrder(_laptop, _quantity);

            //act and assert
            Assert.Throws<InvalidOperationException>(act);
        }

        /// <summary>
        /// Verifies that the inventory reduction method is called
        /// when an order is successfully created.
        /// The ReduceStock method should be invoked.
        /// </summary>
        [Fact]
        public void CreateOrder_ValidOrder_CallsReduceStock()
        {
            //arrange
            SetupSuccessfulDependencies(_laptop, _quantity);
            _inventoryMock.Setup(x => x.ReduceStock(_laptop, _quantity)).Verifiable();

            //act
            _service.CreateOrder(_laptop, _quantity);

            //assert
            _inventoryMock.Verify();
        }

        /// <summary>
        /// Verifies that payment processing is not performed
        /// when there is insufficient stock.
        /// The ProcessPayment method should never be called.
        /// </summary>
        [Fact]
        public void CreateOrder_InsufficientStock_DoesNotCallPayment()
        {
            //arrange
            _inventoryMock.Setup(x => x.CheckStock(_laptop, _quantity)).Returns(false);

            //act
            Record.Exception(() => _service.CreateOrder(_laptop, _quantity));

            //assert
            _paymentMock.Verify(x => x.ProcessPayment(It.IsAny<Order>()), Times.Never());
        }

        /// <summary>
        /// Verifies that a confirmation notification is sent after successful order creation.
        /// The SendConfirmation method should be called exactly once.
        /// </summary>
        [Fact]
        public void CreateOrder_ValidOrder_SendsConfirmationExactlyOnce()
        {
            //arrange
            SetupSuccessfulDependencies(_laptop, _quantity);

            //act
            _service.CreateOrder(_laptop, _quantity);

            //assert
            _notificationMock.Verify(x => x.SendConfirmation(It.IsAny<Order>()), Times.Exactly(1));
        }

        /// <summary>
        /// Verifies that the correct order details are passed to the payment service.
        /// The order should contain the expected product and quantity.
        /// </summary>
        [Fact]
        public void CreateOrder_ValidOrder_PassesCorrectOrderToPayment()
        {
            //arrange
            SetupSuccessfulDependencies(_laptop, _quantity);

            //act
            _service.CreateOrder(_laptop, _quantity);

            //assert
            _paymentMock.Verify(x => x.ProcessPayment(It.Is<Order>(
                                    order => 
                                    order.Product == _laptop && 
                                    order.Quantity == _quantity)), Times.Once());
        }

        /// <summary>
        /// Verifies that removing an existing order calls IncreaseStock
        /// with the correct product and quantity exactly once.
        /// </summary>
        [Fact]
        public void RemoveOrder_ExistingOrder_IncreasesStockOnce()
        {
            //arrange
            SetupSuccessfulDependencies(_laptop, _quantity);

            var order = _service.CreateOrder(_laptop, _quantity);

            //act
            _service.RemoveOrder(order.Id);

            //assert
            _inventoryMock.Verify(x => x.IncreaseStock(_laptop, _quantity), Times.Once());
        }

        /// <summary>
        /// Verifies that an order cannot be updated with a non-positive quantity.
        /// The UpdateOrder method should return false.
        /// </summary>
        [Theory]
        [InlineData(_zeroQuantity)]
        [InlineData(_negativeQuantity)]
        public void UpdateOrder_NonPositiveQuantity_ReturnsFalse(int newQuantity)
        {
            //arrange
            SetupSuccessfulDependencies(_laptop, _quantity);

            var order = _service.CreateOrder(_laptop, _quantity);

            //act
            var actual = _service.UpdateOrder(order.Id, newQuantity);

            //assert
            Assert.False(actual);
        }
        
        /// <summary>
        /// Verifies that an existing order is removed from the order collection.
        /// The collection should no longer contain the removed order.
        /// </summary>
        [Fact]
        public void RemoveOrder_ExistingOrder_RemovesOrderFromCollection()
        {
            //arrange
            SetupSuccessfulDependencies(_laptop, _quantity);

            var order = _service.CreateOrder(_laptop, _quantity);

            //act
            _service.RemoveOrder(order.Id);
            var actual = _service.GetOrders();

            //assert
            Assert.DoesNotContain(order, actual);
        }
    }
}
