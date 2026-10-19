using VaultShop.Utility;

namespace VaultShop.Web.Tests
{
    public class GranMendozaLocalitiesTests
    {
        [Fact]
        public void GranMendozaLocalities_Has26CanonicalEntries()
        {
            Assert.Equal(26, SD.GranMendozaLocalities.Count);
            Assert.Equal(26, SD.GranMendozaLocalities.Distinct().Count());
        }

        // envio-gratis 1.2 — catalog spellings vary in case/accents
        [Theory]
        [InlineData("LAS CANAS")]
        [InlineData("Las Cañas")]
        [InlineData("las cañas")]
        public void IsGranMendoza_CaseAndAccentInsensitive(string locality)
        {
            Assert.True(SD.IsGranMendoza(locality));
        }

        [Theory]
        [InlineData("KILOMETRO 11")]
        [InlineData("kilómetro 11")]
        public void IsGranMendoza_AccentedVariant(string locality)
        {
            Assert.True(SD.IsGranMendoza(locality));
        }

        [Theory]
        [InlineData("Córdoba")]
        [InlineData("Buenos Aires")]
        [InlineData("Chacras")]
        [InlineData("Villa Nueva Sur")]
        [InlineData("Godoy Cruz Centro")]
        [InlineData("")]
        [InlineData(null)]
        public void IsGranMendoza_NonListedLocality_NeverMatches(string? locality)
        {
            Assert.False(SD.IsGranMendoza(locality));
        }

        // envio-gratis 1.2 — new delivery modality codes
        [Fact]
        public void DeliveryTypeConstants_AreDistinct()
        {
            Assert.Equal("S", SD.DeliveryTypePickup);
            Assert.Equal("M", SD.DeliveryTypeMotorcycle);
            Assert.Equal("V", SD.DeliveryTypeVendorPickup);
        }
    }
}
