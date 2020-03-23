using CmsData;
using CmsData.Codes;
using LumenWorks.Framework.IO.Csv;
using System;
using System.IO;
using System.Linq;
using UtilityExtensions;

namespace CmsWeb.Areas.Finance.Models.BatchImport
{
    internal class ChaseImporter : IContributionBatchImporter
    {
        public int? RunImport(string text, DateTime date, int? fundid, bool fromFile)
        {
            using (var csv = new CsvReader(new StringReader(text), true))
            {
                return RunImport(csv, date, fundid);
            }
        }

        public static int? RunImport(CsvReader csv, DateTime date, int? fundid)
        {
            var db = DbUtil.Db;
            var prevbundle = -1;
            var curbundle = 0;
            BundleHeader bh = null;

            var fieldCount = csv.FieldCount;
            var cols = csv.GetFieldHeaders();

            while (csv.ReadNextRecord())
            {
                var bd = new BundleDetail
                {
                    CreatedBy = db.UserId,
                    CreatedDate = DateTime.Now
                };
                var qf = from f in db.ContributionFunds
                         where f.FundStatusId == 1
                         orderby f.FundId
                         select f.FundId;

                bd.Contribution = new Contribution
                {
                    CreatedBy = db.UserId,
                    CreatedDate = DateTime.Now,
                    ContributionDate = date,
                    FundId = fundid ?? qf.First(),
                    ContributionStatusId = 0,
                    ContributionTypeId = ContributionTypeCode.CheckCash
                };
                string ac = null, rt = null, ck = null;
                for (var c = 1; c < fieldCount; c++)
                {
                    switch (cols[c])
                    {
                        case "DEPOSIT NUMBER":
                            curbundle = csv[c].ToInt();
                            if (curbundle != prevbundle)
                            {
                                if (bh != null)
                                {
                                    BatchImportContributions.FinishBundle(bh);
                                }

                                bh = BatchImportContributions.GetBundleHeader(date, DateTime.Now);
                                prevbundle = curbundle;
                            }
                            break;
                        case "AMOUNT":
                            bd.Contribution.ContributionAmount = csv[c].GetAmount();
                            break;
                        case "CHECK NUMBER":
                            ck = csv[c];
                            break;
                        case "ROUTING NUMBER":
                            rt = csv[c];
                            break;
                        case "ACCOUNT NUMBER":
                            ac = csv[c];
                            break;
                    }
                }
                if (!ck.HasValue())
                {
                    if (ac.Contains(' '))
                    {
                        var a = ac.SplitStr(" ", 2);
                        ck = a[0];
                        ac = a[1];
                    }
                }

                var eac = Util.Encrypt(rt + "|" + ac);
                var q = from kc in db.CardIdentifiers
                        where kc.Id == eac
                        select kc.PeopleId;
                var pid = q.SingleOrDefault();
                if (pid != null)
                {
                    bd.Contribution.PeopleId = pid;
                }

                bd.Contribution.BankAccount = eac;
                bd.Contribution.CheckNo = ck;
                bh.BundleDetails.Add(bd);
            }
            BatchImportContributions.FinishBundle(bh);
            return bh.BundleHeaderId;
        }
    }
}
