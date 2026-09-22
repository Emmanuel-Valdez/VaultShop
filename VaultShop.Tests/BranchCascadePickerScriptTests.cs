namespace VaultShop.Web.Tests
{
	public class BranchCascadePickerScriptTests
	{
		// Pins the 6.2 browser finding: the picker's inline script runs before later DOM
		// (e.g. the submit button) is parsed, so the submit gate must re-query the button
		// on every check and re-run once parsing finishes — never cache a null reference.
		[Fact]
		public void PickerScript_GatesSubmitAfterDomParse()
		{
			var script = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
				"..", "..", "..", "..", "VaultShop.Web", "Views", "Shared", "_BranchCascadePicker.cshtml")));

			Assert.Contains("document.getElementById(submitButtonId)", script);
			Assert.Contains("DOMContentLoaded", script);
			Assert.DoesNotContain("var submitBtn = document.getElementById(root.dataset.submitButtonId);", script);
		}
	}
}
