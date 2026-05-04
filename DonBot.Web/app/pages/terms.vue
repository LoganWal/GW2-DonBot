<template>
  <div class="legal-page">
    <h1>Terms of Service</h1>
    <p class="last-updated">Last updated: May 2025</p>

    <section>
      <h2>1. About DonBot</h2>
      <p>DonBot is a Discord bot and web application for Guild Wars 2 communities. It processes combat log data, manages guild roles, tracks player statistics, and provides leaderboards for PvE and WvW content.</p>
    </section>

    <section>
      <h2>2. Acceptance of Terms</h2>
      <p>By using DonBot or this website, you agree to these Terms of Service. If you do not agree, do not use the service.</p>
    </section>

    <section>
      <h2>3. User Responsibilities</h2>
      <ul>
        <li>You must provide accurate Guild Wars 2 API keys that belong to you.</li>
        <li>You must not use DonBot for any illegal or abusive purpose.</li>
        <li>You are responsible for the content you upload (combat log files).</li>
        <li>You must not attempt to disrupt, overload, or exploit the service.</li>
      </ul>
    </section>

    <section>
      <h2>4. Uploaded Content</h2>
      <p>Combat log files (.zevtc) you upload are processed to extract fight statistics. Processed data is stored in our database. Raw files are deleted after processing. By uploading, you grant DonBot permission to process and store the resulting statistics.</p>
    </section>

    <section>
      <h2>5. Third-Party Services</h2>
      <p>DonBot integrates with third-party services including dps.report and GW2 Wingman for log hosting and analysis. Use of these services is subject to their own terms. You control whether your logs are submitted to these services.</p>
    </section>

    <section>
      <h2>6. Service Availability</h2>
      <p>DonBot is provided on a best-effort basis with no uptime guarantees. The service may be modified, interrupted, or discontinued at any time without notice.</p>
    </section>

    <section>
      <h2>7. Limitation of Liability</h2>
      <p>DonBot is provided "as is" without any warranty. We are not liable for any loss of data, service interruptions, or other damages arising from use of the service.</p>
    </section>

    <section>
      <h2>8. Changes to Terms</h2>
      <p>These terms may be updated at any time. Continued use of the service after changes constitutes acceptance of the updated terms.</p>
    </section>

    <section>
      <h2>9. Contact</h2>
      <p>For questions about these terms, contact: <a href="mailto:Logan.Security@proton.me">Logan.Security@proton.me</a></p>
    </section>
  </div>
</template>

<style scoped>
.legal-page {
  max-width: 720px;
}

.legal-page h1 {
  font-size: 1.75rem;
  font-weight: 700;
  margin-bottom: 0.25rem;
}

.last-updated {
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
  margin-bottom: 2rem;
}

section {
  margin-bottom: 1.75rem;
}

h2 {
  font-size: 1.05rem;
  font-weight: 600;
  margin-bottom: 0.5rem;
}

p, li {
  font-size: 0.9rem;
  line-height: 1.65;
  color: var(--p-text-muted-color);
}

ul {
  padding-left: 1.25rem;
}

li {
  margin-bottom: 0.25rem;
}

a {
  color: var(--p-primary-color);
  text-decoration: none;
}

a:hover {
  text-decoration: underline;
}
</style>
