using NugetForUnity;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;

public class TargetFrameworkMonikerComparerTests
{
    [TestCase("net45", "netstandard1.3")]
    [TestCase("netstandard1.3", "net461")]

    public void Should_return_negative_value_when_lhs_version_is_less_than_rhs(string lhs, string rhs)
    {
        //Arrange
        var comparer = new TargetFrameworkMonikerComparer();

        //Act
        var result = comparer.Compare(lhs, rhs);

        //Assert
        Assert.Negative(result);
    }

    [TestCase("net461", "netstandard1.3")]
    [TestCase("netstandard1.3", "net45")]
    [TestCase("netstandard2.0", "netstandard1.3")]
    public void Should_return_positive_value_when_lhs_version_is_greater_than_rhs(string lhs, string rhs)
    {
        //Arrange
        var comparer = new TargetFrameworkMonikerComparer();

        //Act
        var result = comparer.Compare(lhs, rhs);

        //Assert
        Assert.Positive(result);
    }

    [TestCase("net46", "net46")]
    [TestCase("netstandard1.3", "netstandard1.3")]
    public void Should_return_zero_when_lhs_version_equals_rhs(string lhs, string rhs)
    {
        //Arrange
        var comparer = new TargetFrameworkMonikerComparer();

        //Act
        var result = comparer.Compare(lhs, rhs);

        //Assert
        Assert.Zero(result);
    }

    [TestCase("net46", "netstandard1.3", ExpectedResult = 1)]
    [TestCase("netstandard1.3", "net46", ExpectedResult = -1)]
    public int Netframework_should_versus_equivalent_netstandard(string lhs, string rhs)
    {
        //Arrange
        var comparer = new TargetFrameworkMonikerComparer();

        //Act
        var result = comparer.Compare(lhs, rhs);

        //Assert
        return result;
    }

    [TestCase("netstandard1.3", "net46", ExpectedResult = 1)]
    [TestCase("net46", "netstandard1.3", ExpectedResult = -1)]
    public int Netstandard_should_versus_equivalent_netframework(string lhs, string rhs)
    {
        //Arrange
        var comparer = new TargetFrameworkMonikerComparer(false);

        //Act
        var result = comparer.Compare(lhs, rhs);

        //Assert
        return result;
    }

    public class SortTest
    {
        [TestCaseSource("TestCases")]
        public void Should_sort_frameworks_from_lower_to_higher(string[] testCase)
        {
            var testCollection = new List<string>(testCase);

            testCollection.Sort(new TargetFrameworkMonikerComparer());

            Assert.AreEqual(new List<string> { "net45", "netstandard1.2", "netstandard1.3", "net461", "unsupported_framework" }, testCollection);
        }

        public static IEnumerable TestCases
        {
            get
            {
                yield return new[] {"netstandard1.3", "netstandard1.2", "net461", "unsupported_framework", "net45"};
                yield return new[] {"net461", "netstandard1.3", "net45", "netstandard1.2", "unsupported_framework"};
                yield return new[] {"netstandard1.3", "net461", "unsupported_framework", "net45", "netstandard1.2"};
                yield return new[] {"net45", "net461", "netstandard1.2", "unsupported_framework", "netstandard1.3"};
                yield return new[] {"net45", "unsupported_framework", "netstandard1.3", "netstandard1.2", "net461"};
                yield return new[] {"netstandard1.2", "net461", "net45", "netstandard1.3", "unsupported_framework"};
            }
        }
    }
}