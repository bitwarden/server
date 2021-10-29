using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Xunit;

namespace Bit.Core.Test.Enums
{
    public class PlanTypeHelperTests
    {
        private static IEnumerable<PlanType> PlanArchetypeArray(PlanType planType) => new PlanType?[] {
            PlanTypeHelper.HasFreePlan(new Organization {PlanType = planType}) ? planType : null,
            PlanTypeHelper.HasFamiliesPlan(new Organization {PlanType = planType}) ? planType : null,
            PlanTypeHelper.HasTeamsPlan(new Organization {PlanType = planType}) ? planType : null,
            PlanTypeHelper.HasEnterprisePlan(new Organization {PlanType = planType}) ? planType : null,
        }.Where(v => v.HasValue).Select(v => (PlanType)v);

        public static IEnumerable<object[]> PlanTypes => Enum.GetValues<PlanType>().Select(p => new object[] { p });
        public static IEnumerable<object[]> PlanTypesExceptCustom =>
            Enum.GetValues<PlanType>().Except(new[] { PlanType.Custom }).Select(p => new object[] { p });

        [Theory]
        [MemberData(nameof(PlanTypesExceptCustom))]
        public void NonCustomPlanTypesBelongToPlanArchetype(PlanType planType)
        {
            Assert.Contains(planType, PlanArchetypeArray(planType));
        }

        [Theory]
        [MemberData(nameof(PlanTypesExceptCustom))]
        public void PlanTypesBelongToOnlyOneArchetype(PlanType planType)
        {
            Console.WriteLine(planType);
            Assert.Single(PlanArchetypeArray(planType));
        }
    }
}
