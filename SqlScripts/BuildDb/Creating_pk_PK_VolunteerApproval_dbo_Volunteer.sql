ALTER TABLE [dbo].[Volunteer] ADD CONSTRAINT [PK_VolunteerApproval] PRIMARY KEY CLUSTERED  ([PeopleId]) ON [PRIMARY]
GO
IF @@ERROR <> 0 SET NOEXEC ON
GO
