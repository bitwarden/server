namespace Bit.AppHost.Configuration;

/// <summary>
/// Gives each <see cref="MonolithService"/> its resource name and the shared resources it depends on.
/// </summary>
/// <remarks>
/// The switch has no discard arm on purpose: CS8509 is a build error in this repo, so a service added without an
/// entry here fails to compile.
/// </remarks>
internal static class MonolithServiceCatalog
{
    extension(MonolithService service)
    {
        /// <summary>
        /// The Aspire resource name, such as <c>eventsProcessor</c>. <c>AdditionalProjects:*:ReferencedBy</c>
        /// entries match against it.
        /// </summary>
        internal string Name => service.Entry.Name;

        /// <summary>
        /// Whether the service gets a reference to the MailCatcher SMTP endpoint.
        /// </summary>
        internal bool SendsMail => service.Entry.SendsMail;

        /// <summary>
        /// Whether the service waits for the Azurite storage emulator before starting.
        /// </summary>
        internal bool UsesStorage => service.Entry.UsesStorage;

        private (string Name, bool SendsMail, bool UsesStorage) Entry => service switch
        {
            MonolithService.Admin => ("admin", true, false),
            MonolithService.Api => ("api", false, true),
            MonolithService.Billing => ("billing", true, false),
            MonolithService.Events => ("events", false, true),
            MonolithService.EventsProcessor => ("eventsProcessor", false, true),
            MonolithService.Icons => ("icons", false, false),
            MonolithService.Identity => ("identity", true, false),
            MonolithService.Notifications => ("notifications", false, true),
            MonolithService.Scim => ("scim", false, false),
            MonolithService.Sso => ("sso", true, false)
        };
    }
}
