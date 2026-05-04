<template>
  <div class="legal-page">
    <h1>Privacy Policy</h1>
    <p class="last-updated">Last updated: May 2025</p>

    <section>
      <h2>1. What We Collect</h2>
      <p>When you use DonBot, we collect and store:</p>
      <ul>
        <li><strong>Discord account data:</strong> your Discord user ID and username, obtained via Discord OAuth2 login.</li>
        <li><strong>Guild Wars 2 API keys:</strong> provided by you to link your GW2 account.</li>
        <li><strong>GW2 account names:</strong> retrieved from the GW2 API using your API key.</li>
        <li><strong>Combat log data:</strong> fight statistics extracted from uploaded .zevtc files or from dps.report/wvw.report URLs you submit. This includes character names, account names, and in-game performance metrics.</li>
        <li><strong>Upload records:</strong> metadata about files and URLs you submit, including timestamps and processing status.</li>
      </ul>
    </section>

    <section>
      <h2>2. How We Use Your Data</h2>
      <ul>
        <li>Assigning and managing Discord guild roles based on your GW2 account data.</li>
        <li>Displaying leaderboards, statistics, and fight history on this website.</li>
        <li>Processing uploaded combat logs for storage and analysis.</li>
        <li>Authenticating you when you log in via Discord.</li>
      </ul>
      <p>We do not sell your data or use it for advertising.</p>
    </section>

    <section>
      <h2>3. Third-Party Services</h2>
      <p>DonBot interacts with the following third-party services on your behalf:</p>
      <ul>
        <li><strong>Discord:</strong> for authentication and role management. Governed by <a href="https://discord.com/privacy" target="_blank" rel="noopener">Discord's Privacy Policy</a>.</li>
        <li><strong>ArenaNet GW2 API:</strong> to verify your GW2 account using the API key you provide.</li>
        <li><strong>dps.report / wvw.report:</strong> combat logs may be uploaded to these services when you choose to do so. Their data handling is governed by their own policies.</li>
        <li><strong>GW2 Wingman:</strong> your dps.report links may be submitted to Wingman when you enable this option. Their data handling is governed by their own policies.</li>
      </ul>
    </section>

    <section>
      <h2>4. Data Storage</h2>
      <p>Your data is stored in a PostgreSQL database. Raw .zevtc upload files are stored temporarily on disk and deleted immediately after processing. We use reasonable security measures to protect stored data.</p>
    </section>

    <section>
      <h2>5. Data Retention</h2>
      <p>Your data is retained for as long as your account is active or until you request deletion. GW2 API keys can be removed at any time via the Accounts page. To request full data deletion, contact us at the address below.</p>
    </section>

    <section>
      <h2>6. Cookies</h2>
      <p>DonBot uses a single session cookie (<code>donbot_token</code>) to keep you logged in. This cookie is required for the service to function and is deleted when you log out.</p>
    </section>

    <section>
      <h2>7. Your Rights</h2>
      <p>You may request access to, correction of, or deletion of your personal data at any time by contacting us. We will respond within 30 days.</p>
    </section>

    <section>
      <h2>8. Changes to This Policy</h2>
      <p>This policy may be updated at any time. Continued use of the service after changes constitutes acceptance of the updated policy.</p>
    </section>

    <section>
      <h2>9. Contact</h2>
      <p>For privacy-related requests or questions: <a href="mailto:Logan.Security@proton.me">Logan.Security@proton.me</a></p>
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

code {
  font-family: monospace;
  font-size: 0.85em;
  background: var(--p-surface-hover);
  padding: 0.1em 0.35em;
  border-radius: 3px;
}
</style>
