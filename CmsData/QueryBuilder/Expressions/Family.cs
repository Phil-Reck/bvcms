﻿/* Author: David Carroll
 * Copyright (c) 2008, 2009 Bellevue Baptist Church 
 * Licensed under the GNU General Public License (GPL v2)
 * you may not use this code except in compliance with the License.
 * You may obtain a copy of the License at http://bvcms.codeplex.com/license 
 */
using System;
using System.Linq;
using System.Linq.Expressions;
using UtilityExtensions;
using CmsData.Codes;

namespace CmsData
{
    public partial class Condition
    {
        internal Expression NumberOfFamilyMembers()
        {
            var cnt = TextValue.ToInt();
            Expression<Func<Person, int>> pred = p => p.Family.People.Count();
            Expression left = Expression.Invoke(pred, parm);
            var right = Expression.Convert(Expression.Constant(cnt), left.Type);
            return Compare(left, right);
        }
        internal Expression NumberOfPrimaryAdults()
        {
            var cnt = TextValue.ToInt();
            Expression<Func<Person, int>> pred = p => p.Family.People.Count(pp => pp.PositionInFamilyId == 10);
            Expression left = Expression.Invoke(pred, parm);
            var right = Expression.Convert(Expression.Constant(cnt), left.Type);
            return Compare(left, right);
        }
        internal Expression HasParents()
        {
            var tf = CodeIds == "1";
            Expression<Func<Person, bool>> pred = p =>
                p.Family.People.Any(m => m.PositionInFamilyId == PositionInFamily.PrimaryAdult && p.PositionInFamilyId == PositionInFamily.Child);
            Expression expr = Expression.Convert(Expression.Invoke(pred, parm), typeof(bool));
            if (!(op == CompareType.Equal && tf))
                expr = Expression.Not(expr);
            return expr;
        }
        internal Expression FamilyHasChildren()
        {
            var tf = CodeIds == "1";
            Expression<Func<Person, bool>> pred = p =>
                p.Family.People.Any(m => (m.Age ?? 0) <= 12 && m.PositionInFamilyId == PositionInFamily.Child);
            Expression expr = Expression.Convert(Expression.Invoke(pred, parm), typeof(bool));
            if (!(op == CompareType.Equal && tf))
                expr = Expression.Not(expr);
            return expr;
        }
        internal Expression FamilyHasChildrenAged()
        {
            var tf = CodeIds == "1";
            Expression<Func<Person, bool>> pred = p =>
                p.Family.People.Any(m => (m.Age ?? 0) <= (Age ?? 0) && m.PositionInFamilyId == PositionInFamily.Child);
            Expression expr = Expression.Convert(Expression.Invoke(pred, parm), typeof(bool));
            if (!(op == CompareType.Equal && tf))
                expr = Expression.Not(expr);
            return expr;
        }
        internal Expression FamilyHasChildrenAged2()
        {
            var range = Quarters.Split('-');
            var tf = CodeIds == "1";
            Expression<Func<Person, bool>> pred = p =>
                p.Family.People.Any(m => (m.Age ?? 0) >= range[0].ToInt() && (m.Age ?? 0) <= range[1].ToInt() && m.PositionInFamilyId == PositionInFamily.Child);
            Expression expr = Expression.Convert(Expression.Invoke(pred, parm), typeof(bool));
            if (!(op == CompareType.Equal && tf))
                expr = Expression.Not(expr);
            return expr;
        }
        internal Expression FamilyHasChildrenAged3()
        {
            var range = Quarters.Split('-');
            Expression<Func<Person, bool>> pred = p =>
                p.Family.People.Any(m =>
                    (m.Age ?? 0) >= range[0].ToInt()
                    && (m.Age ?? 0) <= range[1].ToInt()
                    && CodeIntIds.Contains(m.GenderId)
                    && m.PositionInFamilyId == PositionInFamily.Child
                );
            Expression expr = Expression.Invoke(pred, parm); // substitute parm for p
            if (op == CompareType.NotEqual || op == CompareType.NotOneOf)
                expr = Expression.Not(expr);
            return expr;
        }
        internal Expression HasRelatedFamily()
        {
            var tf = CodeIds == "1";
            Expression<Func<Person, bool>> pred = p =>
                p.Family.RelatedFamilies1.Any()
                || p.Family.RelatedFamilies2.Any();
            Expression expr = Expression.Convert(Expression.Invoke(pred, parm), typeof(bool));
            if (!(op == CompareType.Equal && tf))
                expr = Expression.Not(expr);
            return expr;
        }
        internal Expression IsHeadOfHousehold()
        {
            var tf = CodeIds == "1";
            Expression<Func<Person, bool>> pred = p =>
                p.Family.HeadOfHouseholdId == p.PeopleId;
            Expression expr = Expression.Convert(Expression.Invoke(pred, parm), typeof(bool));
            if (!(op == CompareType.Equal && tf))
                expr = Expression.Not(expr);
            return expr;
        }
        internal Expression FamHasPrimAdultChurchMemb()
        {
            var tf = CodeIds == "1";
            Expression<Func<Person, bool>> pred = p =>
                p.Family.People.Any(m =>
                    m.PositionInFamilyId == PositionInFamily.PrimaryAdult
                    && m.MemberStatusId == 10 // church member
                    //&& m.PeopleId != p.PeopleId // someone else in family
                    );
            Expression left = Expression.Invoke(pred, parm);
            var right = Expression.Convert(Expression.Constant(tf), left.Type);
            return Compare(left, right);
        }
    }
}
