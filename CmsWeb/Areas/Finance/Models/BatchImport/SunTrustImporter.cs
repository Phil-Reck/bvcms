using CmsData;
using CmsData.Codes;
using LumenWorks.Framework.IO.Csv;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UtilityExtensions;

namespace CmsWeb.Areas.Finance.Models.BatchImport
{
    internal class SunTrustImporter : IContributionBatchImporter
    {
        public int? RunImport(string text, DateTime date, int? fundid, bool fromFile)
        {
            using (var csv = new CsvReader(new StringReader(text), true))
            {
                return BatchProcessSunTrust(csv, date, fundid);
            }
        }

        private static int? BatchProcessSunTrust(CsvReader csv, DateTime date, int? fundid)
        {
            var db = DbUtil.Db;
            var userId = db.UserId;
            var prevbundle = -1;
            var curbundle = 0;

            var bh = BatchImportContributions.GetBundleHeader(date, DateTime.Now);

            var fieldCount = csv.FieldCount;
            var cols = csv.GetFieldHeaders();

            while (csv.ReadNextRecord())
            {
                var bd = new BundleDetail
                {
                    CreatedBy = userId,
                    CreatedDate = DateTime.Now,
                };
                var qf = from f in db.ContributionFunds
                         where f.FundStatusId == 1
                         orderby f.FundId
                         select f.FundId;

                bd.Contribution = new Contribution
                {
                    CreatedBy = userId,
                    CreatedDate = DateTime.Now,
                    ContributionDate = date,
                    FundId = fundid ?? qf.First(),
                    ContributionStatusId = 0,
                    ContributionTypeId = ContributionTypeCode.CheckCash,
                };
                string ac = null, rt = null, ck = null, sn = null;
                for (var c = 1; c < fieldCount; c++)
                {
                    switch (cols[c].ToLower())
                    {
                        case "deposit_id":
                            curbundle = csv[c].ToInt();
                            if (curbundle != prevbundle)
                            {
                                if (curbundle == 3143)
                                {
                                    foreach (var i in bh.BundleDetails)
                                    {
                                        Debug.WriteLine(i.Contribution.ContributionDesc);
                                        Debug.WriteLine(i.Contribution.BankAccount);
                                    }
                                }

                                BatchImportContributions.FinishBundle(bh);
                                bh = BatchImportContributions.GetBundleHeader(date, DateTime.Now);
                                prevbundle = curbundle;
                            }
                            break;
                        case "amount":
                            bd.Contribution.ContributionAmount = csv[c].GetAmount();
                            break;
                        case "tran_code":
                            ck = csv[c];
                            break;
                        case "serial_number":
                            sn = csv[c];
                            break;
                        case "routing_transit":
                            rt = csv[c];
                            break;
                        case "account_number":
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

                bd.Contribution.ContributionDesc = string.Join(" ", sn, ck);
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
                bh.BundleDetails.Add(bd);
            }
            BatchImportContributions.FinishBundle(bh);
            return bh.BundleHeaderId;
        }
    }
}
