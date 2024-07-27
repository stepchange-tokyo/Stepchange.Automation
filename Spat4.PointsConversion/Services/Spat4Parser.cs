using HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.Web;

namespace Spat4.PointsConversion.Services;

internal static partial class Spat4Parser
{
    public const string HTMLTARGET_MAIN_LOGIN_FORM_SESSION_ID = "//form[@id='f_login']//input[@name='sid']";
    public static readonly string HTMLTARGET_LOGIN_FORM_CONTAINER = "//form[@id='f_login']";
    public static readonly string HTMLTARGET_CONFIRM_EXCHANGE_FORM_CONTAINER = "//form[@id='f_send']";

    public const string HTMLTARGET_META_REFRESH_TAG = "//meta[@http-equiv='refresh']";
    public const string META_CONTENT_EXTRA_VALUES = "0; url=";

    public static readonly string HTMLTARGET_CURRENT_POINTS_BALANCE_CONTAINER = "//div[contains(@class, 'validPointHead')]//dl[contains(@class, 'validPoint')]//dd//strong";

    public static readonly string[] TEXT_USER_LOGGEDOUT = { "ログインはこちら" };
    public static readonly string HTMLTARGET_TEXT_EXCHANGE_FOR_CASH = "現金と交換する";
    public static readonly string HTMLTARGET_TEXT_USE_POINTS = "7,17500000,100000,CK000007";

    public static string ParseSessionId(string htmlString)
    {
        var sessionId = string.Empty;
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlString);

        var targetInput = doc.DocumentNode.SelectSingleNode(HTMLTARGET_MAIN_LOGIN_FORM_SESSION_ID);
        if (targetInput != null)
        {
            sessionId = targetInput.GetAttributeValue("value", string.Empty).Trim();
        }

        return sessionId;
    }

    public static string ParseMetaContent(string htmlString)
    {
        string metaContent = string.Empty;
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlString);

        var targetInput = doc.DocumentNode.SelectSingleNode(HTMLTARGET_META_REFRESH_TAG);
        if (targetInput != null)
        {
            metaContent = targetInput.GetAttributeValue("content", string.Empty).Trim();
        }

        return metaContent;
    }

    public static Dictionary<string, string> GetKeys(string metaContent)
    {
        var keyValuePairs = new Dictionary<string, string>();
        var queryStringParameters = HttpUtility.ParseQueryString(metaContent.Replace(META_CONTENT_EXTRA_VALUES, "").Replace("/spat4/pp?", ""));
        foreach (var key in queryStringParameters.AllKeys)
        {
            keyValuePairs.Add(key, queryStringParameters[key]);
        }

        return keyValuePairs;
    }

    public static long? ParsePointsBalance(string htmlString)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlString);
        var pointsTag = doc.DocumentNode.SelectNodes(HTMLTARGET_CURRENT_POINTS_BALANCE_CONTAINER).FirstOrDefault();
        if (pointsTag != null)
        {
            var data = pointsTag.InnerText.Trim().Replace("<br>", "<br />").Replace("<br/>", "<br />");
            var sanitizedValue = NonNumericRegex().Replace(data, string.Empty);

            return long.Parse(sanitizedValue);
        }

        return null;
    }

    public static bool CheckLoggedIn(string htmlString)
    {
        // If the "you are not logged in" message shows up or we are on the main login page, then we are not authenticated.
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlString);
        var loginForm = doc.DocumentNode.SelectSingleNode(HTMLTARGET_LOGIN_FORM_CONTAINER);
        return loginForm == null && !TEXT_USER_LOGGEDOUT.Any(htmlString.Contains);
    }

    public static bool CheckExchangeFormContainer(string htmlString)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlString);

        var targetInput = doc.DocumentNode.SelectSingleNode(HTMLTARGET_CONFIRM_EXCHANGE_FORM_CONTAINER);
        return targetInput != null;
    }

    public static bool IsExchangeForCashPage(string htmlString) => htmlString.Contains(HTMLTARGET_TEXT_EXCHANGE_FOR_CASH);
    public static bool IsUsePointsPage(string htmlString) => htmlString.Contains(HTMLTARGET_TEXT_USE_POINTS);

    [GeneratedRegex("[^.0-9]")]
    private static partial Regex NonNumericRegex();
}
