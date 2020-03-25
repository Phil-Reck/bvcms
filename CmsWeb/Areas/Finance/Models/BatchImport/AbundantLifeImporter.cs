using System;
using System.IO;
using CmsData;
using CmsData.Codes;
using LumenWorks.Framework.IO.Csv;
using UtilityExtensions;

namespace CmsWeb.Areas.Finance.Models.BatchImport
{
    internal class AbundantLifeImporter : IContributionBatchImporter
    {
        enum Columns
        {
            FundedDate,
            TransNumber,
            TransDate,
            Payer,
            Designation,
            Payment,
            Last4,
            GrossAmount,
            FeesWithheld,
            NetAmount,
            Misc,
            MerchantOrderNumber,
            BatchId
        };
        

        public int? RunImport(string text, DateTime date, int? fundid, bool fromFile)
        {
            using (var csv = new CsvReader(new StringReader(text), true))
                return BatchProcessAbundantLife(csv, date, fundid);
        }

        private static int? BatchProcessAbundantLife(CsvReader csv, DateTime date, int? fundid)
        {
            var db = DbUtil.Db;
            BundleHeader bh = null;
            csv.MissingFieldAction = MissingFieldAction.ReplaceByEmpty;

            var fid = fundid ?? BatchImportContributions.FirstFundId();

            var prevbatch = "";

            while (csv.ReadNextRecord())
            {
                if (!csv[Columns.TransNumber.ToInt()].HasValue())
                {
                    continue; // skip summary rows
                }

                var batch = csv[Columns.BatchId.ToInt()];
                if (bh == null || batch != prevbatch)
                {
                    if (bh != null) {
                        BatchImportContributions.FinishBundle(bh);
                    }
                    bh = BatchImportContributions.GetBundleHeader(date, DateTime.Now);
                    prevbatch = batch;
                }

                var amount = csv[Columns.GrossAmount.ToInt()];

                var bd = new BundleDetail
                {
                    CreatedBy = db.UserId,
                    CreatedDate = DateTime.Now
                };

                bd.Contribution = new Contribution
                {
                    CreatedBy = db.UserId,
                    CreatedDate = DateTime.Now,
                    ContributionDate = date,
                    FundId = fid,
                    ContributionStatusId = ContributionStatusCode.Recorded,
                    ContributionTypeId = ContributionTypeCode.CheckCash,
                    ContributionAmount = amount.GetAmount()
                };

                bh.BundleDetails.Add(bd);
            }

            BatchImportContributions.FinishBundle(bh);

            return bh?.BundleHeaderId;
        }
    }
}
