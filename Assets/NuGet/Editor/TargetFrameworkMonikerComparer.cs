using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NugetForUnity
{
    /// <summary>
    /// Compare target frameworks monikers
    /// </summary>
    /// <see cref="https://docs.microsoft.com/en-us/nuget/reference/target-frameworks"/>
    public class TargetFrameworkMonikerComparer : IComparer<string>
    {
        /// <see cref="https://docs.microsoft.com/en-us/dotnet/standard/net-standard"/>
        private readonly Dictionary<int, float> _netFrameworkApiCompatibilityMap = new Dictionary<int, float>
        {
            { 45, 1.1F},
            { 451, 1.2F },
            { 46, 1.3F},
            { 461, 2.0F }
        };

        private readonly Regex _netFrameworkExpression = new Regex(@"net(\d+)");
        private readonly Regex _netStandardExpression = new Regex(@"netstandard((\d*\.)?\d+)");

        private readonly bool _preferNetFramework;

        public TargetFrameworkMonikerComparer(bool preferNetFramework = true)
        {
            _preferNetFramework = preferNetFramework;
        }

        public int Compare(string lhs, string rhs)
        {
            if (lhs == null)
            {
                throw new ArgumentNullException(lhs);
            }

            if (rhs == null)
            {
                throw new ArgumentNullException(rhs);
            }

            var lhsMatchNetStandard = _netStandardExpression.Match(lhs);
            var lhsMatchNetFramework = _netFrameworkExpression.Match(lhs);
            var rhsMatchNetStandard = _netStandardExpression.Match(rhs);
            var rhsMatchNetFramework = _netFrameworkExpression.Match(rhs);

            float lhsNetStandard;
            float rhsNetStandard;

            if (lhsMatchNetStandard.Success)
            {
                lhsNetStandard = float.Parse(lhsMatchNetStandard.Groups[1].Value);
            }
            else if (lhsMatchNetFramework.Success)
            {
                var lhsNetFramework = int.Parse(lhsMatchNetFramework.Groups[1].Value);

                if (!_netFrameworkApiCompatibilityMap.TryGetValue(lhsNetFramework, out lhsNetStandard))
                {
                    return 1;
                }
            }
            else
            {
                return 1;
            }

            if (rhsMatchNetStandard.Success)
            {
                rhsNetStandard = float.Parse(rhsMatchNetStandard.Groups[1].Value);
            }
            else if (rhsMatchNetFramework.Success)
            {
                var rhsNetFramework = int.Parse(rhsMatchNetFramework.Groups[1].Value);

                if (!_netFrameworkApiCompatibilityMap.TryGetValue(rhsNetFramework, out rhsNetStandard))
                {
                    return -1;
                }
            }
            else
            {
                return -1;
            }

            var netStandardCompareResult = lhsNetStandard.CompareTo(rhsNetStandard);
            if (netStandardCompareResult == 0 && lhsMatchNetStandard.Success ^ rhsMatchNetStandard.Success) // both operands targets same version of net standard
            {
                return lhsMatchNetFramework.Success
                    ? _preferNetFramework ? 1 : -1
                    : _preferNetFramework ? -1 : 1;
            }

            return netStandardCompareResult;
        }
    }
}