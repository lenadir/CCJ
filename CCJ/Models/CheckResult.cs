namespace CCJ.Models
{
    /// <summary>One line of a connectivity or credential report.</summary>
    public class CheckResult
    {
        public string Label { get; set; }
        public bool Ok { get; set; }
        public string Detail { get; set; }

        public CheckResult()
        {
        }

        public CheckResult(string label, bool ok, string detail)
        {
            Label = label;
            Ok = ok;
            Detail = detail;
        }

        public override string ToString()
        {
            return (Ok ? "[ OK ]   " : "[ FAIL ] ") + Label + ": " + Detail;
        }
    }
}
