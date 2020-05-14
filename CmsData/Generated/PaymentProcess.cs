using CmsData.Infrastructure;
using System.ComponentModel;
using System.Data.Linq;
using System.Data.Linq.Mapping;

namespace CmsData
{
    [Table(Name = "dbo.PaymentProcess")]
    public partial class PaymentProcess : INotifyPropertyChanging, INotifyPropertyChanged
    {
        private static PropertyChangingEventArgs emptyChangingEventArgs => new PropertyChangingEventArgs("");

        #region Private Fields

        private int _ProcessId;
        private string _ProcessName;
        private int? _GatewayAccountId;
        private bool _AcceptACH = true;
        private bool _AcceptCredit = true;
        private bool _AcceptDebit = true;

        private EntityRef<GatewayAccount> _GatewayAccount;
        #endregion

        #region Extensibility Method Definitions

        partial void OnLoaded();
        partial void OnValidate(System.Data.Linq.ChangeAction action);
        partial void OnCreated();

        partial void OnProcessIdChanging(int value);
        partial void OnProcessIdChanged();

        partial void OnProcessNameChanging(string value);
        partial void OnProcessNameChanged();

        partial void OnGatewayAccountIdChanging(int? value);
        partial void OnGatewayAccountIdChanged();

        partial void OnAcceptACHChanging(bool value);
        partial void OnAcceptACHChanged();

        partial void OnAcceptCreditChanging(bool value);
        partial void OnAcceptCreditChanged();

        partial void OnAcceptDebitChanging(bool value);
        partial void OnAcceptDebitChanged();

        #endregion

        public PaymentProcess()
        {
            OnCreated();
        }

        #region Columns

        [Column(Name = "ProcessId", UpdateCheck = UpdateCheck.Never, Storage = "_ProcessId", AutoSync = AutoSync.OnInsert, DbType = "int IDENTITY", IsPrimaryKey = true, IsDbGenerated = true)]
        public int ProcessId
        {
            get => _ProcessId;

            set
            {
                if (_ProcessId != value)
                {
                    OnProcessIdChanging(value);
                    SendPropertyChanging();
                    _ProcessId = value;
                    SendPropertyChanged("ProcessId");
                    OnProcessIdChanged();
                }
            }
        }

        [Column(Name = "ProcessName", UpdateCheck = UpdateCheck.Never, Storage = "_ProcessName", AutoSync = AutoSync.OnInsert, DbType = "nvarchar NOT NULL")]
        public string ProcessName
        {
            get => _ProcessName;

            set
            {
                if (_ProcessName != value)
                {
                    OnProcessNameChanging(value);
                    SendPropertyChanging();
                    _ProcessName = value;
                    SendPropertyChanged("ProcessName");
                    OnProcessNameChanged();
                }
            }
        }

        [Column(Name = "GatewayAccountId", UpdateCheck = UpdateCheck.Never, Storage = "_GatewayAccountId", DbType = "int NOT NULL")]
        [IsForeignKey]
        public int? GatewayAccountId
        {
            get => _GatewayAccountId;

            set
            {
                if (_GatewayAccountId != value)
                {
                    if (_GatewayAccount.HasLoadedOrAssignedValue)
                    {
                        throw new System.Data.Linq.ForeignKeyReferenceAlreadyHasValueException();
                    }

                    OnGatewayAccountIdChanging(value);
                    SendPropertyChanging();
                    _GatewayAccountId = value;
                    SendPropertyChanged("GatewayAccountId");
                    OnGatewayAccountIdChanged();
                }
            }
        }

        [Column(Name = "AcceptACH", UpdateCheck = UpdateCheck.Never, Storage = "_AcceptACH", AutoSync = AutoSync.OnInsert, DbType = "bit NOT NULL")]
        public bool AcceptACH
        {
            get => _AcceptACH;

            set
            {
                if (_AcceptACH != value)
                {
                    OnAcceptACHChanging(value);
                    SendPropertyChanging();
                    _AcceptACH = value;
                    SendPropertyChanged("AcceptACH");
                    OnAcceptACHChanged();
                }
            }
        }

        [Column(Name = "AcceptCredit", UpdateCheck = UpdateCheck.Never, Storage = "_AcceptCredit", AutoSync = AutoSync.OnInsert, DbType = "bit NOT NULL")]
        public bool AcceptCredit
        {
            get => _AcceptCredit;

            set
            {
                if (_AcceptCredit != value)
                {
                    OnAcceptCreditChanging(value);
                    SendPropertyChanging();
                    _AcceptCredit = value;
                    SendPropertyChanged("AcceptCredit");
                    OnAcceptCreditChanged();
                }
            }
        }

        [Column(Name = "AcceptDebit", UpdateCheck = UpdateCheck.Never, Storage = "_AcceptDebit", AutoSync = AutoSync.OnInsert, DbType = "bit NOT NULL")]
        public bool AcceptDebit
        {
            get => _AcceptDebit;

            set
            {
                if (_AcceptDebit != value)
                {
                    OnAcceptDebitChanging(value);
                    SendPropertyChanging();
                    _AcceptDebit = value;
                    SendPropertyChanged("AcceptDebit");
                    OnAcceptDebitChanged();
                }
            }
        }

        #endregion

        #region Foreign Keys

        #endregion

        public event PropertyChangingEventHandler PropertyChanging;
        protected virtual void SendPropertyChanging()
        {
            PropertyChanging?.Invoke(this, emptyChangingEventArgs);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void SendPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
