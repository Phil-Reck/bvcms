using System;
using System.Linq;
using System.Web;
using CmsData;
using UtilityExtensions;

namespace CmsWeb.Areas.Search.Models
{
    public partial class QueryModel
    {
        public static bool ClipboardHasCondition()
        {
            var clip = Util.QueryClipboard;
            return clip != null;
        }

        public void Copy()
        {
            Util.QueryClipboard = Selected.ToXml(newGuids: true);
        }
        public void Cut()
        {
            Util.QueryClipboard = Selected.ToXml();
            Selected.DeleteClause();
            TopClause.Save(Db, increment: true);
        }

        public void Paste()
        {
            var clip = Util.QueryClipboard;
            if (clip == null)
                return;
            var newclause = Condition.Import(clip);

            foreach(var c in newclause.AllConditions)
                Selected.AllConditions[c.Key] = c.Value;;
            newclause.AllConditions = Selected.AllConditions;

            if (Selected.IsGroup)
            {
                newclause.Order = Selected.MaxClauseOrder() + 2;
                newclause.ParentId = Selected.Id;
            }
            else
            {
                newclause.Order = Selected.Order + 1;
                newclause.ParentId = Selected.Parent.Id;
                Selected.Parent.ReorderClauses();
            }
            TopClause.Save(Db, increment: true);
        }
        public Guid AddConditionToGroup()
        {
            var nc = Selected.AddNewClause();
            TopClause.Save(Db);
            return nc.Id;
        }
        public Guid AddGroupToGroup()
        {
            var g = Selected.AddNewGroupClause();
            var nc = g.AddNewClause();
            TopClause.Save(Db);
            return nc.Id;
        }
        public Condition Selected
        {
            get
            {
                if (!SelectedId.HasValue || !TopClause.AllConditions.ContainsKey(SelectedId.Value))
                {
                    throw (new Exception($"selected gid: {SelectedId} not found"));
                }
                return TopClause.AllConditions[SelectedId.Value];
            }
        }
        public void DeleteCondition()
        {
            Selected.DeleteClause();
            TopClause.Save(Db, increment: true);
        }
        public void InsertGroupAbove()
        {
            var g = new Condition
            {
                Id = Guid.NewGuid(),
                ConditionName = QueryType.Group.ToString(),
                Comparison = CompareType.AnyTrue.ToString(),
                AllConditions = Selected.AllConditions
            };
            if (Selected.IsFirst)
            {
                Selected.ParentId = g.Id;
                g.ParentId = null;
            }
            else
            {
                var list = Selected.Parent.Conditions.Where(cc => cc.Order >= Selected.Order).ToList();
                g.ParentId = Selected.ParentId;
                foreach (var c in list)
                    c.ParentId = g.Id;
                g.Order = Selected.MaxClauseOrder();
            }
            Selected.AllConditions.Add(g.Id, g);
            if (g.IsFirst)
            {
                // g will now becojme the new TopClause
                g.Description = TopClause.Description;

                // swap TopClauseId with the new GroupId so the saved query will have the same id
                var tcid = TopClause.Id;
                var gid = g.Id;
                var conditions = TopClause.Conditions.ToList();
                TopClause.Id = gid;
                foreach (var c in conditions)
                    c.ParentId = gid;
                g.Id = tcid;
                TopClause.ParentId = g.Id;
                TopClause = g;
                TopClause.Save(Db);
            }
            TopClause.Save(Db, increment: true);
        }
        public void MakeTopGroup()
        {
            Selected.Description = TopClause.Description;
            var conditions = Selected.Conditions.ToList();
            foreach (var c in conditions)
                c.ParentId = TopClause.Id;
            Selected.Id = TopClause.Id;
            Selected.ParentId = null;
            TopClause = Selected;
            TopClause.Save(Db);
        }
        public void ToggleConditionEnabled()
        {
            var tc = TopClause; // forces read
            Selected.DisableOnScratchpad = !Selected.DisableOnScratchpad;
            tc.Save(Db);
        }
    }
}
