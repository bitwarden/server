# Bitwarden and Open Source

The source code for all Bitwarden software products is hosted on GitHub and we welcome everyone to review, audit, and contribute to the Bitwarden codebase.

We believe that making our source code open and available is a defining feature of Bitwarden, and that source code transparency offers critically important customer benefits for security solutions like Bitwarden.

As an open solution, Bitwarden publishes the source code for various modules under different licenses.  We're providing this License Statement and FAQ document as an overview of our licensing philosophy, the specifics of module licensing, and to answer common questions regarding our licenses.

# Bitwarden Software Licensing

We have two tiers of licensing for our software. The core products are offered under one of the GPL open source licenses: GPL 3 and  A-GPL 3. A select number of features, primarily those designed for use by larger organizations rather than individuals and families, are licensed under a "Source Available" commercial license [here](https://github.com/bitwarden/server/blob/master/LICENSE_BITWARDEN.txt).

Our current software products have the following licenses:

*Bitwarden clients:* The core password management code for individual password vaults, including Desktop, Web, Browser, Mobile, and CLI versions, is available under the GPL 3.0 license.

*Bitwarden server:* The main Bitwarden server code is licensed under the AGPL 3.0 license.

*Commercial.Core and SSO integration:* Code for certain new modules that are designed and developed for use by larger
organizations and enterprise environments is released under the Bitwarden License, a "source available" license. The
Bitwarden License provides users access to product source code for non-production purposes such as development and
testing, but requires a paid subscription for production use of the product, and environments supporting production.
Additionally the Api module by default includes Commercial.Core which is under the Bitwarden License, however this can
be disabled by using `/p:DefineConstants="OSS"` as an argument to `dotnet` while building the module.

# Frequently Asked Questions

***How can I contribute to Bitwarden open source projects?***

We welcome new members of our developer community and there are many ways for you to contribute to our projects. For more information visit our [Community Resources](https://community.bitwarden.com/), specifically our Forum on GitHub Contributions.

***In your GitHub repositories, how can I determine what license applies to a given software program?***

Each Bitwarden repository contains a `LICENSE.txt` file that spells out which license applies to the code in that repository.

In the case of the [Bitwarden server repository](https://github.com/bitwarden/server), the files are organized into various directories. These directories are not only used for logical code organization, but also to clearly distinguish the license that a given source file falls under. All source files under the `/bitwarden_license` directory at the root of the server repository are subject to the Bitwarden License. If a file is not organized under the `/bitwarden_license` directory, the AGPL 3.0 license applies.

***Can I offer a managed service based on Bitwarden products?***

Any individual or organization considering offering Bitwarden "as a service" must be mindful of the strong "copyleft" attributes of our open source licenses, as well as the Bitwarden License. With respect to the server software available under the Bitwarden License, production use requires a separate commercial agreement with Bitwarden. With respect to the server software available under the AGPL license, as software professionals we cannot conceive a scenario in which the offering of Bitwarden "as-a-service" would not involve a modification to the applicable Bitwarden code, thereby triggering the strong copyleft provisions of the AGPL 3.0 license. We encourage anyone considering offering Bitwarden as a service to join the Bitwarden Partner Program for access to the comprehensive resources and support we make available to our authorized solutions partners. Please [contact us](https://bitwarden.com/contact/) for information.

***What rights do I receive under the "Source Available" Bitwarden License?*** 

Users of software licensed under the Bitwarden License receive a right to use the software source code for non-production purposes of internal development and internal testing. The right to use the software in a production environment, or environments directly supporting production, requires a paid Bitwarden subscription. This approach is modeled on the licensing approaches taken by other successful open source companies including Elastic (NYSE: ESTC) and Confluent (NASDAQ: CFLT).

***Is Bitwarden open source?***

As detailed above, the Bitwarden password management clients for individual use, the main Bitwarden server, and many libraries are available under the GPL family of licenses. The GPL licenses are widely used open source licenses created by the Free Software Foundation and endorsed as "open source" by the [Open Source Initiative](https://opensource.org/history). The Bitwarden License does not qualify as an open source license under the OSI definition, but we believe that the license successfully balances the principles of openness and community with our business goals.

***If I redistribute or provide services related to Bitwarden open source software can I use the "Bitwarden" name?***

Our licenses do not grant any rights in the trademarks, service marks, or logos of Bitwarden (except as may be necessary to comply with the notice requirements as applicable). The Bitwarden trademark is a trusted mark applied to products distributed by Bitwarden, Inc., owner of the Bitwarden trademarks and products. We have adopted and enforce strict rules governing use of our trademarks. Use of any Bitwarden trademarks must comply with Bitwarden [Trademark Guidelines](https://github.com/bitwarden/server/blob/master/TRADEMARK_GUIDELINES.md).

***Bitwarden Trademark Usage***

Because Open Source plays a major part in how we build our products, we see it as a matter of course to give the same effort back to our community by creating valuable, free and easy-to-use software. We need to make sure our trademarks remain distinctive so you know what you're getting and from who.

***Do I need permission to use the Bitwarden Trademarks?***

You don't need permission to use our marks when truthfully referring to our products, services or features, or to explain that your products or services are based on our open- source code so long as not misleading. Any other use requires our permission.

***How should I use the Bitwarden Trademarks when allowed?***

Use the Bitwarden Trademarks exactly as [shown](https://github.com/bitwarden/server/blob/master/TRADEMARK_GUIDELINES.md) and without modification. For example, do not abbreviate, hyphenate, or remove elements and separate them from surrounding text, images and other features. Always use the Bitwarden Trademarks as adjectives followed by a generic term, never as a noun or verb.

Use the Bitwarden Trademarks only to reference one of our products or services, but never in a way that implies sponsorship or affiliation by Bitwarden. For example, do not use any part of the Bitwarden Trademarks as the name of your business, product or service name, application, domain name, publication or other offering – this can be confusing to others.

***Where can I find more information?***

For more information on how to use the Bitwarden Trademarks, including in connection with self-hosted options and open-source code, see our [Trademark Guidelines](https://github.com/bitwarden/server/blob/master/TRADEMARK_GUIDELINES.md) or [contacts us](https://bitwarden.com/contact/).
