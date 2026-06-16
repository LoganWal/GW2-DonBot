<template>
  <div>
    <ProgressSpinner v-if="pending" />

    <template v-else-if="result">
      <div v-if="hideLogsTab && singleLog" style="margin-bottom: 1.25rem;">
        <div style="display: flex; align-items: baseline; gap: 0.75rem; flex-wrap: wrap; margin-bottom: 0.75rem;">
          <h2 style="margin: 0; font-size: 1.25rem; font-weight: 600;">{{ fightName(singleLog.fightType) }}</h2>
          <span style="color: var(--p-text-muted-color); font-size: 0.875rem;">
            {{ new Date(singleLog.fightStart).toLocaleString() }} · {{ formatDuration(singleLog.fightDurationInMs) }}
          </span>
          <Tag
            v-if="singleLog.fightType !== 0"
            :severity="singleLog.isSuccess ? 'success' : 'danger'"
            :value="singleLog.isSuccess ? 'Kill' : `${singleLog.fightPercent}% — Wipe`"
          />
          <Tag v-else severity="secondary" value="WvW" />
          <Button
            v-if="singleLog.url"
            label="View on dps.report"
            icon="pi pi-external-link"
            size="small"
            severity="secondary"
            outlined
            as="a"
            :href="singleLog.url"
            target="_blank"
            rel="noopener"
            style="margin-left: auto;"
          />
        </div>
        <div v-if="displayResult" style="display: flex; gap: 1rem; flex-wrap: wrap; align-items: stretch;">
          <StatCard label="Fight Time" :value="formatDuration(displayResult.totalDurationMs, true)" />
          <StatCard label="Type" :value="displayResult.type === 'wvw' ? 'WvW' : 'PvE'" />
          <StatCard label="Players" :value="displayResult.players.length" />
          <StatCard v-if="displayResult.type === 'wvw' && enemyData" label="Enemy Count" :value="(enemyData.totalTargets ?? 0).toLocaleString()" />
          <template v-if="displayResult.type !== 'wvw'">
            <StatCard label="Group DPS" :value="groupDps.toLocaleString()" />
            <StatCard label="Avg Quick" :value="`${avgQuick.toFixed(1)}%`" />
            <StatCard label="Avg Alac" :value="`${avgAlac.toFixed(1)}%`" />
            <StatCard label="Avg Quick Gen" :value="`${avgQuickGen.toFixed(1)}%`" />
            <StatCard label="Avg Alac Gen" :value="`${avgAlacGen.toFixed(1)}%`" />
          </template>
          <template v-else>
            <StatCard label="Damage Dealt" :value="wvwTotals.damage.toLocaleString()" />
            <StatCard label="Damage Taken" :value="wvwTotals.damageTaken.toLocaleString()" />
            <StatCard label="Enemies Killed" :value="wvwTotals.kills.toLocaleString()" />
            <StatCard label="Enemies Downed" :value="wvwTotals.downs.toLocaleString()" />
            <StatCard label="Group Deaths" :value="wvwTotals.deaths.toLocaleString()" />
            <StatCard label="Group Downed" :value="wvwTotals.timesDowned.toLocaleString()" />
            <StatCard label="Avg Quick Gen" :value="`${avgQuickGen.toFixed(1)}%`" />
            <StatCard label="Avg Alac Gen" :value="`${avgAlacGen.toFixed(1)}%`" />
          </template>
        </div>
      </div>

      <div v-if="displayResult && !hideLogsTab" style="display: flex; gap: 1rem; flex-wrap: wrap; margin-bottom: 1.5rem; align-items: stretch;">
        <StatCard label="Logs" :value="displayResult.totalLogs" />
        <StatCard label="Fight Time" :value="formatDuration(displayResult.totalDurationMs, true)" />
        <StatCard v-if="displayResult.sessionDurationMs" label="Total Time" :value="formatDuration(displayResult.sessionDurationMs, true)" />
        <StatCard v-if="displayResult.sessionDurationMs && displayResult.sessionDurationMs > displayResult.totalDurationMs" label="Downtime" :value="formatDuration(displayResult.sessionDurationMs - displayResult.totalDurationMs, true)" />
        <StatCard label="Type" :value="displayResult.type === 'wvw' ? 'WvW' : 'PvE'" />
        <StatCard label="Players" :value="displayResult.players.length" />
        <StatCard v-if="displayResult.type === 'wvw' && enemyData" label="Enemy Count" :value="(enemyData.totalTargets ?? 0).toLocaleString()" />
        <template v-if="displayResult.type === 'wvw'">
          <StatCard label="Win / Loss" :value="`${wvwWinLoss.wins}W / ${wvwWinLoss.losses}L`" />
          <StatCard label="Damage Dealt" :value="wvwTotals.damage.toLocaleString()" />
          <StatCard label="Damage Taken" :value="wvwTotals.damageTaken.toLocaleString()" />
          <StatCard label="Enemies Killed" :value="wvwTotals.kills.toLocaleString()" />
          <StatCard label="Enemies Downed" :value="wvwTotals.downs.toLocaleString()" />
          <StatCard label="Group Deaths" :value="wvwTotals.deaths.toLocaleString()" />
          <StatCard label="Group Downed" :value="wvwTotals.timesDowned.toLocaleString()" />
          <StatCard label="Avg Quick Gen" :value="`${avgQuickGen.toFixed(1)}%`" />
          <StatCard label="Avg Alac Gen" :value="`${avgAlacGen.toFixed(1)}%`" />
        </template>
        <template v-else>
          <StatCard label="Kills / Wipes" :value="`${pveKillsWipes.kills}K / ${pveKillsWipes.wipes}W`" />
          <StatCard label="Group DPS" :value="groupDps.toLocaleString()" />
          <StatCard label="Avg Quick" :value="`${avgQuick.toFixed(1)}%`" />
          <StatCard label="Avg Alac" :value="`${avgAlac.toFixed(1)}%`" />
          <StatCard label="Avg Quick Gen" :value="`${avgQuickGen.toFixed(1)}%`" />
          <StatCard label="Avg Alac Gen" :value="`${avgAlacGen.toFixed(1)}%`" />
        </template>
        <ProgressSpinner v-if="filterPending" style="width: 2rem; height: 2rem;" />
      </div>

      <div v-if="result.type !== 'wvw' && !hideLogsTab" style="display: flex; gap: 1rem; flex-wrap: wrap; margin-bottom: 0.75rem; align-items: center;">
        <FilterButtonGroup
          :options="successFilterOptions"
          :model-value="aggSuccessFilter"
          @update:model-value="aggSuccessFilter = $event"
        />
        <FilterButtonGroup
          :options="difficultyFilterOptions"
          :model-value="aggDifficultyFilter"
          @update:model-value="aggDifficultyFilter = $event"
        />
      </div>

      <div v-if="displayResult && displayResult.players?.length > 0" style="display: flex; gap: 0.5rem; flex-wrap: wrap; align-items: center; margin-bottom: 0.75rem;">
        <span style="font-size: 0.8rem; color: var(--p-text-muted-color);">
          {{ selectedPlayers.length }} of {{ displayResult.players.length }} players selected
        </span>
        <Button
          label="Select All"
          icon="pi pi-check-square"
          size="small"
          severity="secondary"
          outlined
          :disabled="selectedPlayers.length === displayResult.players.length"
          @click="selectAllPlayers"
        />
        <Button
          label="Unselect All"
          icon="pi pi-stop"
          size="small"
          severity="secondary"
          outlined
          :disabled="selectedPlayers.length === 0"
          @click="unselectAllPlayers"
        />
      </div>

      <Tabs v-model:value="activeTab" class="tabs-with-toggles">
        <div class="tab-toggles">
          <Button
            :label="showGraphs ? 'Hide Graphs' : 'Show Graphs'"
            :icon="showGraphs ? 'pi pi-eye-slash' : 'pi pi-eye'"
            size="small"
            severity="secondary"
            outlined
            @click="showGraphs = !showGraphs"
          />
          <Button
            :label="showTables ? 'Hide Table' : 'Show Table'"
            :icon="showTables ? 'pi pi-eye-slash' : 'pi pi-eye'"
            size="small"
            severity="secondary"
            outlined
            @click="showTables = !showTables"
          />
        </div>
        <TabList>
          <Tab v-if="!hideLogsTab" value="logs">Logs</Tab>
          <Tab value="damage">Damage</Tab>
          <Tab value="support">Support</Tab>
          <Tab value="survivability">Survivability</Tab>
          <Tab value="points">Points Earned</Tab>
          <Tab v-if="displayResult?.type !== 'wvw'" value="mechanics">Mechanics</Tab>
          <Tab v-if="displayResult?.type === 'wvw'" value="enemy">Know My Enemy</Tab>
        </TabList>
        <TabPanels>

          <TabPanel v-if="!hideLogsTab" value="logs">
            <DataTable :value="filteredAggLogs" striped-rows class="mb-section" size="small" :row-style="logRowStyle" @row-click="onLogRowClick">
              <Column header="Fight">
                <template #body="{ data }">{{ fightName(data.fightType) }}</template>
              </Column>
              <Column header="Date">
                <template #body="{ data }">{{ new Date(data.fightStart)?.toLocaleString() ?? '0' }}</template>
              </Column>
              <Column header="Duration">
                <template #body="{ data }">{{ formatDuration(data.fightDurationInMs) }}</template>
              </Column>
              <Column header="Result">
                <template #body="{ data }">
                  <Tag v-if="data.fightType !== 0" :severity="data.isSuccess ? 'success' : 'danger'" :value="data.isSuccess ? 'Kill' : `${data.fightPercent}%`" />
                  <Tag v-else severity="secondary" value="WvW" />
                </template>
              </Column>
              <Column header="Links" style="width: 6rem;">
                <template #body="{ data }">
                  <div style="display: flex; gap: 0.5rem; align-items: center;">
                    <Button icon="pi pi-eye" severity="secondary" text size="small" v-tooltip.top="rowActionTooltip" @click.stop="onRowAction(data)" />
                    <a v-if="data.url" :href="data.url" target="_blank" rel="noopener" v-tooltip.top="'Open on dps.report'" style="color: var(--p-text-muted-color); display: flex; align-items: center;" @click.stop>
                      <i class="pi pi-external-link" style="font-size: 0.875rem;" />
                    </a>
                  </div>
                </template>
              </Column>
            </DataTable>
            <Message v-if="filteredAggLogs.length === 0" severity="info" :closable="false">
              No logs match the current filter.
            </Message>
          </TabPanel>

          <TabPanel value="damage">
            <template v-if="displayResult && filteredAggLogs.length > 0 && displayResult.type === 'wvw'">
              <div v-if="showGraphs && (chartHasData(wvwDamageChartData) || chartHasData(wvwDdcChartData) || chartHasData(killsChartData) || chartHasData(downsChartData))" class="charts-row mb-section">
                <div v-if="chartHasData(wvwDamageChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Damage per Fight</div>
                  <Chart :type="chartType" :data="wvwDamageChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(wvwDdcChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">DDC per Fight</div>
                  <Chart :type="chartType" :data="wvwDdcChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(killsChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Kills per Fight</div>
                  <Chart :type="chartType" :data="killsChartData" :options="clickableIntChartOptions" />
                </div>
                <div v-if="chartHasData(downsChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Downs per Fight</div>
                  <Chart :type="chartType" :data="downsChartData" :options="clickableIntChartOptions" />
                </div>
              </div>
              <DataTable v-if="showTables" :value="displayResult.players" striped-rows scrollable class="mb-section" sort-field="damage" :sort-order="-1" data-key="accountName" :selection="selectedPlayers" @update:selection="onSelectionChange">
                <Column selection-mode="multiple" header-style="width: 3rem" frozen />
                <Column field="fightCount" :sortable="true" style="width: 40px; min-width: 40px;" header-style="width: 40px">
                  <template #header><i class="pi pi-hashtag" v-tooltip.top="'Fights'" /></template>
                </Column>
                <Column field="subGroup" :sortable="true" style="width: 40px; min-width: 40px;" header-style="width: 40px">
                  <template #header><i class="pi pi-users" v-tooltip.top="'Subgroup'" /></template>
                </Column>
                <Column field="accountName" header="Account" :sortable="true" frozen style="min-width: 160px;" />
                <Column header="Role" :sortable="true" sort-field="playstyle" style="min-width: 115px;">
                  <template #body="{ data }">
                    <Tag v-if="playstyleLabel(data)" :severity="playstyleSeverity(data)" :value="playstyleLabel(data)" v-tooltip.top="playstyleTooltip(data) ?? undefined" />
                    <span v-else class="points-muted">-</span>
                  </template>
                </Column>
                <Column header="Damage" :sortable="true" sort-field="damage" style="min-width: 95px;">
                  <template #body="{ data }">{{ data.damage?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="DDC" :sortable="true" sort-field="damageDownContribution" style="min-width: 85px;">
                  <template #body="{ data }">{{ data.damageDownContribution?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column field="kills" header="Kills" :sortable="true" style="min-width: 60px;" />
                <Column field="downs" header="Downs" :sortable="true" style="min-width: 65px;" />
                <ColumnGroup type="footer">
                  <Row v-for="row in damageWvwSummary" :key="row.key" :class="{ 'summary-total': row.isTotal }">
                    <Column footer="" />
                    <Column footer="" />
                    <Column :footer="row.subGroupLabel" />
                    <Column :footer="row.rowLabel" />
                    <Column footer="" />
                    <Column :footer="fmtN(row.damage)" />
                    <Column :footer="fmtN(row.damageDownContribution)" />
                    <Column :footer="fmtN(row.kills)" />
                    <Column :footer="fmtN(row.downs)" />
                  </Row>
                </ColumnGroup>
              </DataTable>
            </template>
            <template v-else-if="displayResult && filteredAggLogs.length > 0">
              <div v-if="showGraphs && (chartHasData(pveDpsChartData) || chartHasData(pveCleaveDpsChartData) || chartHasData(pveQuickChartData) || chartHasData(pveAlacChartData))" class="charts-row mb-section">
                <div v-if="chartHasData(pveDpsChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">DPS per Fight</div>
                  <Chart :type="chartType" :data="pveDpsChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(pveCleaveDpsChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Cleave DPS per Fight</div>
                  <Chart :type="chartType" :data="pveCleaveDpsChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(pveQuickChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Quickness % per Fight</div>
                  <Chart :type="chartType" :data="pveQuickChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(pveAlacChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Alacrity % per Fight</div>
                  <Chart :type="chartType" :data="pveAlacChartData" :options="clickableChartOptions" />
                </div>
              </div>
              <DataTable v-if="showTables" :value="displayResult.players" striped-rows scrollable class="mb-section" sort-field="dps" :sort-order="-1" data-key="accountName" :selection="selectedPlayers" @update:selection="onSelectionChange">
                <Column selection-mode="multiple" header-style="width: 3rem" frozen />
                <Column field="fightCount" :sortable="true" style="width: 40px; min-width: 40px;" header-style="width: 40px">
                  <template #header><i class="pi pi-hashtag" v-tooltip.top="'Fights'" /></template>
                </Column>
                <Column field="subGroup" :sortable="true" style="width: 40px; min-width: 40px;" header-style="width: 40px">
                  <template #header><i class="pi pi-users" v-tooltip.top="'Subgroup'" /></template>
                </Column>
                <Column field="accountName" header="Account" :sortable="true" frozen style="min-width: 160px;" />
                <Column header="Role" :sortable="true" sort-field="playstyle" style="min-width: 115px;">
                  <template #body="{ data }">
                    <Tag v-if="playstyleLabel(data)" :severity="playstyleSeverity(data)" :value="playstyleLabel(data)" v-tooltip.top="playstyleTooltip(data) ?? undefined" />
                    <span v-else class="points-muted">-</span>
                  </template>
                </Column>
                <Column header="DPS" :sortable="true" sort-field="dps" style="min-width: 90px;">
                  <template #body="{ data }">{{ data.dps?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Cleave DPS" :sortable="true" sort-field="cleaveDps" style="min-width: 105px;">
                  <template #body="{ data }">{{ data.cleaveDps?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Quick %" :sortable="true" sort-field="quicknessDuration" style="min-width: 80px;">
                  <template #body="{ data }">{{ data.quicknessDuration }}%</template>
                </Column>
                <Column header="Alac %" :sortable="true" sort-field="alacDuration" style="min-width: 75px;">
                  <template #body="{ data }">{{ data.alacDuration }}%</template>
                </Column>
                <ColumnGroup type="footer">
                  <Row v-for="row in damagePveSummary" :key="row.key" :class="{ 'summary-total': row.isTotal }">
                    <Column footer="" />
                    <Column footer="" />
                    <Column :footer="row.subGroupLabel" />
                    <Column :footer="row.rowLabel" />
                    <Column footer="" />
                    <Column :footer="fmtN(row.dps)" />
                    <Column :footer="fmtN(row.cleaveDps)" />
                    <Column :footer="fmtPct(row.quicknessDuration)" />
                    <Column :footer="fmtPct(row.alacDuration)" />
                  </Row>
                </ColumnGroup>
              </DataTable>
            </template>
          </TabPanel>

          <TabPanel value="support">
            <template v-if="displayResult && filteredAggLogs.length > 0 && displayResult.type === 'wvw'">
              <div v-if="showGraphs && (chartHasData(healingChartData) || chartHasData(barrierGenChartData) || chartHasData(stabOnChartData) || chartHasData(stabOffChartData) || chartHasData(cleansesChartData) || chartHasData(wvwBoonsRippedChartData) || chartHasData(stripsChartData) || chartHasData(interruptsChartData) || chartHasData(wvwQuickChartData) || chartHasData(wvwQuickGenChartData) || chartHasData(wvwAlacGenChartData))" class="charts-row mb-section">
                <div v-if="chartHasData(healingChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Healing per Fight</div>
                  <Chart :type="chartType" :data="healingChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(barrierGenChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Barrier Gen per Fight</div>
                  <Chart :type="chartType" :data="barrierGenChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(stabOnChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Stab On Gen per Fight</div>
                  <Chart :type="chartType" :data="stabOnChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(stabOffChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Stab Off Gen per Fight</div>
                  <Chart :type="chartType" :data="stabOffChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(cleansesChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Cleanses per Fight</div>
                  <Chart :type="chartType" :data="cleansesChartData" :options="clickableIntChartOptions" />
                </div>
                <div v-if="chartHasData(wvwBoonsRippedChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Boons Ripped per Fight</div>
                  <Chart :type="chartType" :data="wvwBoonsRippedChartData" :options="clickableIntChartOptions" />
                </div>
                <div v-if="chartHasData(stripsChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Strips per Fight</div>
                  <Chart :type="chartType" :data="stripsChartData" :options="clickableIntChartOptions" />
                </div>
                <div v-if="chartHasData(interruptsChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Interrupts per Fight</div>
                  <Chart :type="chartType" :data="interruptsChartData" :options="clickableIntChartOptions" />
                </div>
                <div v-if="chartHasData(wvwQuickChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Quickness % per Fight</div>
                  <Chart :type="chartType" :data="wvwQuickChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(wvwQuickGenChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Quick Gen % per Fight</div>
                  <Chart :type="chartType" :data="wvwQuickGenChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(wvwAlacGenChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Alac Gen % per Fight</div>
                  <Chart :type="chartType" :data="wvwAlacGenChartData" :options="clickableChartOptions" />
                </div>
              </div>
              <DataTable v-if="showTables" :value="displayResult.players" striped-rows scrollable class="mb-section" sort-field="healing" :sort-order="-1" data-key="accountName" :selection="selectedPlayers" @update:selection="onSelectionChange">
                <Column selection-mode="multiple" header-style="width: 3rem" frozen />
                <Column field="fightCount" :sortable="true" style="width: 40px; min-width: 40px;" header-style="width: 40px">
                  <template #header><i class="pi pi-hashtag" v-tooltip.top="'Fights'" /></template>
                </Column>
                <Column field="subGroup" :sortable="true" style="width: 40px; min-width: 40px;" header-style="width: 40px">
                  <template #header><i class="pi pi-users" v-tooltip.top="'Subgroup'" /></template>
                </Column>
                <Column field="accountName" header="Account" :sortable="true" frozen style="min-width: 160px;" />
                <Column header="Role" :sortable="true" sort-field="playstyle" style="min-width: 115px;">
                  <template #body="{ data }">
                    <Tag v-if="playstyleLabel(data)" :severity="playstyleSeverity(data)" :value="playstyleLabel(data)" v-tooltip.top="playstyleTooltip(data) ?? undefined" />
                    <span v-else class="points-muted">-</span>
                  </template>
                </Column>
                <Column header="Healing" :sortable="true" sort-field="healing" style="min-width: 105px;">
                  <template #body="{ data }">{{ data.healing?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Barrier Gen" :sortable="true" sort-field="barrierGenerated" style="min-width: 120px;">
                  <template #body="{ data }">{{ data.barrierGenerated?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column :sortable="true" sort-field="stabOnGroup" style="min-width: 100px;">
                  <template #header><span v-tooltip.top="'Stability generation on your own subgroup'">Stab On Gen</span></template>
                  <template #body="{ data }">{{ data.stabOnGroup }}</template>
                </Column>
                <Column :sortable="true" sort-field="stabOffGroup" style="min-width: 100px;">
                  <template #header><span v-tooltip.top="'Stability generation on players in other subgroups'">Stab Off Gen</span></template>
                  <template #body="{ data }">{{ data.stabOffGroup }}</template>
                </Column>
                <Column header="Quick Gen" :sortable="true" sort-field="quicknessGenGroup" style="min-width: 90px;">
                  <template #body="{ data }">{{ data.quicknessGenGroup }}%</template>
                </Column>
                <Column header="Alac Gen" :sortable="true" sort-field="alacGenGroup" style="min-width: 85px;">
                  <template #body="{ data }">{{ data.alacGenGroup }}%</template>
                </Column>
                <Column header="Cleanses" :sortable="true" sort-field="cleanses" style="min-width: 95px;">
                  <template #body="{ data }">{{ data.cleanses?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column field="numberOfBoonsRipped" header="Boons Ripped" :sortable="true" style="min-width: 110px;" />
                <Column header="Strips" :sortable="true" sort-field="strips" style="min-width: 80px;">
                  <template #body="{ data }">{{ data.strips?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column field="interrupts" header="Interrupts" :sortable="true" style="min-width: 90px;" />
                <Column header="Quick %" :sortable="true" sort-field="quicknessDuration" style="min-width: 80px;">
                  <template #body="{ data }">{{ data.quicknessDuration }}%</template>
                </Column>
                <ColumnGroup type="footer">
                  <Row v-for="row in supportWvwSummary" :key="row.key" :class="{ 'summary-total': row.isTotal }">
                    <Column footer="" />
                    <Column footer="" />
                    <Column :footer="row.subGroupLabel" />
                    <Column :footer="row.rowLabel" />
                    <Column footer="" />
                    <Column :footer="fmtN(row.healing)" />
                    <Column :footer="fmtN(row.barrierGenerated)" />
                    <Column :footer="fmtDec(row.stabOnGroup)" />
                    <Column :footer="fmtDec(row.stabOffGroup)" />
                    <Column :footer="fmtPct(row.quicknessGenGroup)" />
                    <Column :footer="fmtPct(row.alacGenGroup)" />
                    <Column :footer="fmtN(row.cleanses)" />
                    <Column :footer="fmtN(row.numberOfBoonsRipped)" />
                    <Column :footer="fmtN(row.strips)" />
                    <Column :footer="fmtN(row.interrupts)" />
                    <Column :footer="fmtPct(row.quicknessDuration)" />
                  </Row>
                </ColumnGroup>
              </DataTable>
            </template>
            <template v-else-if="displayResult && filteredAggLogs.length > 0">
              <div v-if="showGraphs && (chartHasData(healingChartData) || chartHasData(barrierGenChartData) || chartHasData(cleansesChartData) || chartHasData(stripsChartData) || chartHasData(stabOnChartData) || chartHasData(stabOffChartData) || chartHasData(pveQuickGenChartData) || chartHasData(pveAlacGenChartData))" class="charts-row mb-section">
                <div v-if="chartHasData(healingChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Healing per Fight</div>
                  <Chart :type="chartType" :data="healingChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(barrierGenChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Barrier Gen per Fight</div>
                  <Chart :type="chartType" :data="barrierGenChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(cleansesChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Cleanses per Fight</div>
                  <Chart :type="chartType" :data="cleansesChartData" :options="clickableIntChartOptions" />
                </div>
                <div v-if="chartHasData(stripsChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Strips per Fight</div>
                  <Chart :type="chartType" :data="stripsChartData" :options="clickableIntChartOptions" />
                </div>
                <div v-if="chartHasData(stabOnChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Stab On Group per Fight</div>
                  <Chart :type="chartType" :data="stabOnChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(stabOffChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Stab Off Group per Fight</div>
                  <Chart :type="chartType" :data="stabOffChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(pveQuickGenChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Quick Gen % per Fight</div>
                  <Chart :type="chartType" :data="pveQuickGenChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(pveAlacGenChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Alac Gen % per Fight</div>
                  <Chart :type="chartType" :data="pveAlacGenChartData" :options="clickableChartOptions" />
                </div>
              </div>
              <DataTable v-if="showTables" :value="displayResult.players" striped-rows scrollable class="mb-section" sort-field="healing" :sort-order="-1" data-key="accountName" :selection="selectedPlayers" @update:selection="onSelectionChange">
                <Column selection-mode="multiple" header-style="width: 3rem" frozen />
                <Column field="fightCount" :sortable="true" style="width: 40px; min-width: 40px;" header-style="width: 40px">
                  <template #header><i class="pi pi-hashtag" v-tooltip.top="'Fights'" /></template>
                </Column>
                <Column field="subGroup" :sortable="true" style="width: 40px; min-width: 40px;" header-style="width: 40px">
                  <template #header><i class="pi pi-users" v-tooltip.top="'Subgroup'" /></template>
                </Column>
                <Column field="accountName" header="Account" :sortable="true" frozen style="min-width: 160px;" />
                <Column header="Role" :sortable="true" sort-field="playstyle" style="min-width: 115px;">
                  <template #body="{ data }">
                    <Tag v-if="playstyleLabel(data)" :severity="playstyleSeverity(data)" :value="playstyleLabel(data)" v-tooltip.top="playstyleTooltip(data) ?? undefined" />
                    <span v-else class="points-muted">-</span>
                  </template>
                </Column>
                <Column header="Healing" :sortable="true" sort-field="healing" style="min-width: 105px;">
                  <template #body="{ data }">{{ data.healing?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Barrier Gen" :sortable="true" sort-field="barrierGenerated" style="min-width: 120px;">
                  <template #body="{ data }">{{ data.barrierGenerated?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Cleanses" :sortable="true" sort-field="cleanses" style="min-width: 95px;">
                  <template #body="{ data }">{{ data.cleanses?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Strips" :sortable="true" sort-field="strips" style="min-width: 80px;">
                  <template #body="{ data }">{{ data.strips?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Stab On" :sortable="true" sort-field="stabOnGroup" style="min-width: 80px;">
                  <template #body="{ data }">{{ data.stabOnGroup }}</template>
                </Column>
                <Column header="Stab Off" :sortable="true" sort-field="stabOffGroup" style="min-width: 80px;">
                  <template #body="{ data }">{{ data.stabOffGroup }}</template>
                </Column>
                <Column header="Quick Gen" :sortable="true" sort-field="quicknessGenGroup" style="min-width: 90px;">
                  <template #body="{ data }">{{ data.quicknessGenGroup }}%</template>
                </Column>
                <Column header="Alac Gen" :sortable="true" sort-field="alacGenGroup" style="min-width: 85px;">
                  <template #body="{ data }">{{ data.alacGenGroup }}%</template>
                </Column>
                <ColumnGroup type="footer">
                  <Row v-for="row in supportPveSummary" :key="row.key" :class="{ 'summary-total': row.isTotal }">
                    <Column footer="" />
                    <Column footer="" />
                    <Column :footer="row.subGroupLabel" />
                    <Column :footer="row.rowLabel" />
                    <Column footer="" />
                    <Column :footer="fmtN(row.healing)" />
                    <Column :footer="fmtN(row.barrierGenerated)" />
                    <Column :footer="fmtN(row.cleanses)" />
                    <Column :footer="fmtN(row.strips)" />
                    <Column :footer="fmtDec(row.stabOnGroup)" />
                    <Column :footer="fmtDec(row.stabOffGroup)" />
                    <Column :footer="fmtPct(row.quicknessGenGroup)" />
                    <Column :footer="fmtPct(row.alacGenGroup)" />
                  </Row>
                </ColumnGroup>
              </DataTable>
            </template>
          </TabPanel>

          <TabPanel value="survivability">
            <template v-if="displayResult && filteredAggLogs.length > 0 && displayResult.type === 'wvw'">
              <div v-if="showGraphs && (chartHasData(deathsChartData) || chartHasData(downedChartData) || chartHasData(firstToDieChartData) || chartHasData(damageTakenChartData) || chartHasData(barrierMitChartData) || chartHasData(resTimeChartData) || chartHasData(timesInterruptedChartData) || tagRadialPoints.length > 0)" class="charts-row mb-section">
                <div v-if="chartHasData(deathsChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Deaths per Fight</div>
                  <Chart :type="chartType" :data="deathsChartData" :options="clickableIntChartOptions" />
                </div>
                <div v-if="chartHasData(downedChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Downed per Fight</div>
                  <Chart :type="chartType" :data="downedChartData" :options="clickableIntChartOptions" />
                </div>
                <div v-if="chartHasData(firstToDieChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Died 1st per Fight</div>
                  <Chart :type="chartType" :data="firstToDieChartData" :options="clickableIntChartOptions" />
                </div>
                <div v-if="chartHasData(damageTakenChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Damage Taken per Fight</div>
                  <Chart :type="chartType" :data="damageTakenChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(barrierMitChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Barrier Mit per Fight</div>
                  <Chart :type="chartType" :data="barrierMitChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(resTimeChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Res Time (s) per Fight</div>
                  <Chart :type="chartType" :data="resTimeChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(timesInterruptedChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Interrupted per Fight</div>
                  <Chart :type="chartType" :data="timesInterruptedChartData" :options="clickableIntChartOptions" />
                </div>
                <div v-if="tagRadialPoints.length > 0" class="chart-container tag-radial-wrap">
                  <div class="chart-label">Distance from Tag</div>
                  <svg viewBox="0 0 400 400" class="tag-radial">
                    <circle v-for="ring in tagRadialRings" :key="ring.r" :cx="200" :cy="200" :r="ring.r" class="tag-radial-ring" />
                    <text v-for="ring in tagRadialRings" :key="`l-${ring.r}`" :x="200" :y="200 - ring.r - 2" class="tag-radial-ring-label">{{ ring.label }}</text>
                    <circle :cx="200" :cy="200" r="6" class="tag-radial-center" />
                    <text :x="200" :y="194" class="tag-radial-center-label">TAG</text>
                    <g v-for="pt in tagRadialPoints" :key="pt.account">
                      <line :x1="200" :y1="200" :x2="pt.x" :y2="pt.y" :stroke="pt.color" stroke-opacity="0.25" stroke-width="1" />
                      <circle
                        :cx="pt.x"
                        :cy="pt.y"
                        :r="hoveredAccount === pt.account ? 7 : 5"
                        :fill="pt.color"
                        stroke="#0f0f14"
                        stroke-width="1.5"
                        class="tag-radial-dot"
                        @mouseenter="onRadialEnter(pt, $event)"
                        @mouseleave="onRadialLeave"
                      />
                      <text :x="pt.labelX" :y="pt.labelY" :text-anchor="pt.anchor" class="tag-radial-label">{{ pt.shortAccount }} · {{ Math.round(pt.distance) }}</text>
                    </g>
                  </svg>
                  <div v-if="radialTooltip" class="tag-radial-tooltip" :style="{ left: radialTooltip.left + 'px', top: radialTooltip.top + 'px' }">
                    <strong>{{ radialTooltip.account }}</strong>
                    <span>{{ Math.round(radialTooltip.distance) }} units</span>
                  </div>
                </div>
              </div>
              <DataTable v-if="showTables" :value="displayResult.players" striped-rows scrollable class="mb-section" sort-field="deaths" :sort-order="-1" data-key="accountName" :selection="selectedPlayers" @update:selection="onSelectionChange">
                <Column selection-mode="multiple" header-style="width: 3rem" frozen />
                <Column field="fightCount" :sortable="true" style="width: 40px; min-width: 40px;" header-style="width: 40px">
                  <template #header><i class="pi pi-hashtag" v-tooltip.top="'Fights'" /></template>
                </Column>
                <Column field="subGroup" :sortable="true" style="width: 40px; min-width: 40px;" header-style="width: 40px">
                  <template #header><i class="pi pi-users" v-tooltip.top="'Subgroup'" /></template>
                </Column>
                <Column field="accountName" header="Account" :sortable="true" frozen style="min-width: 160px;" />
                <Column header="Role" :sortable="true" sort-field="playstyle" style="min-width: 115px;">
                  <template #body="{ data }">
                    <Tag v-if="playstyleLabel(data)" :severity="playstyleSeverity(data)" :value="playstyleLabel(data)" v-tooltip.top="playstyleTooltip(data) ?? undefined" />
                    <span v-else class="points-muted">-</span>
                  </template>
                </Column>
                <Column field="deaths" header="Deaths" :sortable="true" style="min-width: 70px;" />
                <Column field="timesDowned" header="Downed" :sortable="true" style="min-width: 70px;" />
                <Column field="firstToDie" header="Died 1st" :sortable="true" style="min-width: 75px;" />
                <Column header="Dmg Taken" :sortable="true" sort-field="damageTaken" style="min-width: 105px;">
                  <template #body="{ data }">{{ data.damageTaken?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Barrier Mit" :sortable="true" sort-field="barrierMitigation" style="min-width: 100px;">
                  <template #body="{ data }">{{ data.barrierMitigation?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Res Time (s)" :sortable="true" sort-field="resurrectionTime" style="min-width: 105px;">
                  <template #body="{ data }">{{ (data.resurrectionTime / 1000).toFixed(1) }}</template>
                </Column>
                <Column field="timesInterrupted" header="Interrupted" :sortable="true" style="min-width: 95px;" />
                <Column header="Dist Tag" :sortable="true" sort-field="distanceFromTag" style="min-width: 80px;">
                  <template #body="{ data }">{{ data.distanceFromTag > 0 ? data.distanceFromTag : '-' }}</template>
                </Column>
                <ColumnGroup type="footer">
                  <Row v-for="row in survivabilityWvwSummary" :key="row.key" :class="{ 'summary-total': row.isTotal }">
                    <Column footer="" />
                    <Column footer="" />
                    <Column :footer="row.subGroupLabel" />
                    <Column :footer="row.rowLabel" />
                    <Column footer="" />
                    <Column :footer="fmtN(row.deaths)" />
                    <Column :footer="fmtN(row.timesDowned)" />
                    <Column :footer="fmtN(row.firstToDie)" />
                    <Column :footer="fmtN(row.damageTaken)" />
                    <Column :footer="fmtN(row.barrierMitigation)" />
                    <Column :footer="fmtSec(row.resurrectionTime)" />
                    <Column :footer="fmtN(row.timesInterrupted)" />
                    <Column :footer="row.distanceFromTag > 0 ? fmtDec(row.distanceFromTag) : '-'" />
                  </Row>
                </ColumnGroup>
              </DataTable>
            </template>

            <template v-else-if="displayResult && filteredAggLogs.length > 0">
              <div v-if="showGraphs && (chartHasData(deathsChartData) || chartHasData(downedChartData) || chartHasData(firstToDieChartData) || chartHasData(damageTakenChartData) || chartHasData(resTimeChartData))" class="charts-row mb-section">
                <div v-if="chartHasData(deathsChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Deaths per Fight</div>
                  <Chart :type="chartType" :data="deathsChartData" :options="clickableIntChartOptions" />
                </div>
                <div v-if="chartHasData(downedChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Downed per Fight</div>
                  <Chart :type="chartType" :data="downedChartData" :options="clickableIntChartOptions" />
                </div>
                <div v-if="chartHasData(firstToDieChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Died 1st per Fight</div>
                  <Chart :type="chartType" :data="firstToDieChartData" :options="clickableIntChartOptions" />
                </div>
                <div v-if="chartHasData(damageTakenChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Damage Taken per Fight</div>
                  <Chart :type="chartType" :data="damageTakenChartData" :options="clickableChartOptions" />
                </div>
                <div v-if="chartHasData(resTimeChartData)" class="chart-container clickable-chart">
                  <div class="chart-label">Res Time (s) per Fight</div>
                  <Chart :type="chartType" :data="resTimeChartData" :options="clickableChartOptions" />
                </div>
              </div>
              <DataTable v-if="showTables" :value="displayResult.players" striped-rows scrollable class="mb-section" sort-field="deaths" :sort-order="-1" data-key="accountName" :selection="selectedPlayers" @update:selection="onSelectionChange">
                <Column selection-mode="multiple" header-style="width: 3rem" frozen />
                <Column field="fightCount" :sortable="true" style="width: 40px; min-width: 40px;" header-style="width: 40px">
                  <template #header><i class="pi pi-hashtag" v-tooltip.top="'Fights'" /></template>
                </Column>
                <Column field="subGroup" :sortable="true" style="width: 40px; min-width: 40px;" header-style="width: 40px">
                  <template #header><i class="pi pi-users" v-tooltip.top="'Subgroup'" /></template>
                </Column>
                <Column field="accountName" header="Account" :sortable="true" frozen style="min-width: 160px;" />
                <Column header="Role" :sortable="true" sort-field="playstyle" style="min-width: 115px;">
                  <template #body="{ data }">
                    <Tag v-if="playstyleLabel(data)" :severity="playstyleSeverity(data)" :value="playstyleLabel(data)" v-tooltip.top="playstyleTooltip(data) ?? undefined" />
                    <span v-else class="points-muted">-</span>
                  </template>
                </Column>
                <Column field="deaths" header="Deaths" :sortable="true" style="min-width: 70px;" />
                <Column field="timesDowned" header="Downed" :sortable="true" style="min-width: 70px;" />
                <Column field="firstToDie" header="Died 1st" :sortable="true" style="min-width: 75px;" />
                <Column header="Dmg Taken" :sortable="true" sort-field="damageTaken" style="min-width: 105px;">
                  <template #body="{ data }">{{ data.damageTaken?.toLocaleString() ?? '0' }}</template>
                </Column>
                <Column header="Res Time (s)" :sortable="true" sort-field="resurrectionTime" style="min-width: 105px;">
                  <template #body="{ data }">{{ (data.resurrectionTime / 1000).toFixed(1) }}</template>
                </Column>
                <ColumnGroup type="footer">
                  <Row v-for="row in survivabilityPveSummary" :key="row.key" :class="{ 'summary-total': row.isTotal }">
                    <Column footer="" />
                    <Column footer="" />
                    <Column :footer="row.subGroupLabel" />
                    <Column :footer="row.rowLabel" />
                    <Column footer="" />
                    <Column :footer="fmtN(row.deaths)" />
                    <Column :footer="fmtN(row.timesDowned)" />
                    <Column :footer="fmtN(row.firstToDie)" />
                    <Column :footer="fmtN(row.damageTaken)" />
                    <Column :footer="fmtSec(row.resurrectionTime)" />
                  </Row>
                </ColumnGroup>
              </DataTable>
            </template>
          </TabPanel>

          <TabPanel value="points">
            <div v-if="pointsSummary" class="points-tab">
              <div class="points-overview">
                <StatCard label="Points Awarded" :value="pointsSummary.totalPoints" />
                <StatCard label="Awarded Players" :value="pointsSummary.awardedPlayers" />
                <StatCard label="Components" :value="pointsSummary.components?.length ?? 0" />
              </div>

              <DataTable v-if="pointsRows.length" :value="pointsRows" striped-rows scrollable size="small" sort-field="totalPoints" :sort-order="-1">
                <Column field="accountName" header="Account" frozen style="min-width: 160px;" />
                <Column field="fightCount" header="Fights" style="width: 5rem;" />
                <Column header="Points" :sortable="true" sort-field="totalPoints" style="width: 7rem;">
                  <template #body="{ data }">{{ formatPointValue(data.totalPoints) }}</template>
                </Column>
                <Column header="Components" style="min-width: 260px;">
                  <template #body="{ data }">
                    <div v-if="data.components?.length" class="point-component-tags">
                      <Tag
                        v-for="component in data.components"
                        :key="component.metric"
                        severity="secondary"
                        :value="`${component.metricLabel}: ${formatPointValue(component.points)}`"
                      />
                    </div>
                    <span v-else class="points-muted">No points earned</span>
                  </template>
                </Column>
                <Column header="Logs" style="min-width: 300px;">
                  <template #body="{ data }">
                    <div v-if="data.logs?.length" class="point-log-stack">
                      <button
                        v-for="log in data.logs"
                        :key="log.fightLogId"
                        class="point-log-row"
                        type="button"
                        @click="onPointLogClick(log.fightLogId)"
                      >
                        <span>{{ fightName(log.fightType) }}</span>
                        <strong>{{ formatPointValue(log.totalPoints) }}</strong>
                      </button>
                    </div>
                    <span v-else class="points-muted">-</span>
                  </template>
                </Column>
              </DataTable>

              <Message v-else severity="secondary" :closable="false">
                No points were awarded for these logs.
              </Message>
            </div>
          </TabPanel>

          <TabPanel value="mechanics">
            <div v-if="mechanicsByGroup.length > 0">
              <div v-for="group in mechanicsByGroup" :key="group.label" class="mechanic-group">
                <CollapsibleSection :title="group.label" :collapsed="false">
                  <div v-for="item in group.items" :key="item.fightType" class="mechanic-fight-wrap">
                    <button class="mechanic-fight-toggle" @click="toggleFight(`${group.label}:${item.fightType}`)">
                      <span>{{ fightName(item.fightType) }}</span>
                      <i :class="openFights.has(`${group.label}:${item.fightType}`) ? 'pi pi-chevron-up' : 'pi pi-chevron-down'" class="mechanic-fight-icon" />
                    </button>
                    <div v-show="openFights.has(`${group.label}:${item.fightType}`)">
                      <DataTable :value="flattenMechanicPlayers(item.players)" striped-rows size="small" scrollable class="mechanic-table">
                        <Column field="accountName" header="Account" frozen style="min-width: 150px;" />
                        <Column v-for="mech in item.mechanicNames" :key="mech" :field="mech" :header="mech" :sortable="true" style="min-width: 80px;" header-style="white-space: nowrap">
                          <template #body="{ data }">
                            <span :class="{ 'mech-zero': !data[mech] }">{{ data[mech] ?? 0 }}</span>
                          </template>
                        </Column>
                      </DataTable>
                    </div>
                  </div>
                </CollapsibleSection>
              </div>
            </div>
            <Message v-else severity="info" :closable="false">No mechanics data available.</Message>
          </TabPanel>

          <TabPanel value="enemy">
            <div class="enemy-pane">
              <ProgressSpinner v-if="enemyLoading" />
              <Message v-if="enemyError" severity="error" :closable="false">{{ enemyError }}</Message>
              <template v-if="enemyData && !enemyLoading">
                <div style="display: flex; gap: 1rem; flex-wrap: wrap; margin-bottom: 0.75rem;">
                  <StatCard label="Logs Processed" :value="enemyData.logsProcessed" />
                  <StatCard label="Total Enemies" :value="enemyData.totalTargets" />
                  <StatCard label="Classes" :value="enemyData.classes?.length ?? 0" />
                </div>
                <DataTable v-if="enemyData.classes?.length" :value="enemyData.classes" striped-rows size="small" sort-field="avgTotal" :sort-order="-1">
                  <Column field="className" header="Class" :sortable="true" />
                  <Column field="count" header="Count" :sortable="true" style="width: 6rem;" />
                  <Column header="Avg Total Dmg" :sortable="true" sort-field="avgTotal">
                    <template #body="{ data }">{{ data.avgTotal?.toLocaleString() ?? '0' }}</template>
                  </Column>
                  <Column header="Avg Strike" :sortable="true" sort-field="avgStrike">
                    <template #body="{ data }">{{ data.avgStrike?.toLocaleString() ?? '0' }}</template>
                  </Column>
                  <Column header="Avg Condi" :sortable="true" sort-field="avgCondi">
                    <template #body="{ data }">{{ data.avgCondi?.toLocaleString() ?? '0' }}</template>
                  </Column>
                </DataTable>
                <Message v-else severity="info" :closable="false">No enemy data found.</Message>
              </template>
            </div>
          </TabPanel>

        </TabPanels>
      </Tabs>
    </template>

    <Message v-else-if="!pending" severity="secondary" :closable="false">
      No data found for the selected logs.
    </Message>
  </div>
</template>

<script setup lang="ts">
import { fightName, groupByFightType, formatDuration } from '~/composables/useFightTypes'
import { successFilterOptions, difficultyFilterOptions, type SuccessFilter, type DifficultyFilter } from '~/composables/useLogFilters'
import CollapsibleSection from '~/components/CollapsibleSection.vue'

type AggregateResult = any

const props = defineProps<{
  fetchAggregate: (logIds?: number[]) => Promise<AggregateResult>
  reloadKey?: number | string
  selectedLogId?: number | null
  rowAction?: 'navigate' | 'select'
  hideLogsTab?: boolean
}>()

const emit = defineEmits<{
  (e: 'select-log', fightLogId: number): void
  (e: 'summary', summary: { type: 'wvw' | 'pve'; wins?: number; losses?: number; kills?: number; wipes?: number } | null): void
}>()

const result = ref<AggregateResult | null>(null)
const displayResult = ref<AggregateResult | null>(null)
const pending = ref(true)
const filterPending = ref(false)
const aggSuccessFilter = ref<SuccessFilter>('all')
const aggDifficultyFilter = ref<DifficultyFilter>(null)
const showGraphs = ref(true)
const showTables = ref(true)
const activeTab = ref<string>('damage')
const unselectedAccounts = ref<Set<string>>(new Set())
const enemyData = ref<any>(null)
const enemyLoading = ref(false)
const enemyError = ref<string | null>(null)
const api = useApi()

const loadEnemyData = async () => {
  enemyLoading.value = true
  enemyError.value = null
  try {
    const logIds = (result.value?.logs ?? []).map((l: any) => l.fightLogId)
    enemyData.value = await api('/api/logs/know-my-enemy', {
      method: 'POST',
      body: { logIds },
    })
  } catch (err: any) {
    enemyError.value = err?.message ?? 'Failed to load enemy data.'
  } finally {
    enemyLoading.value = false
  }
}

watch(() => props.reloadKey, () => {
  enemyData.value = null
  enemyError.value = null
})

watch(result, (r) => {
  if (r?.type === 'wvw' && !enemyData.value && !enemyLoading.value) {
    loadEnemyData()
  }
  if (!r) {
    emit('summary', null)
    return
  }
  if (r.type === 'wvw') {
    emit('summary', { type: 'wvw', wins: wvwWinLoss.value.wins, losses: wvwWinLoss.value.losses })
  } else {
    emit('summary', { type: 'pve', kills: pveKillsWipes.value.kills, wipes: pveKillsWipes.value.wipes })
  }
}, { immediate: true })

const singleLog = computed(() => {
  const logs = result.value?.logs ?? []
  return logs.length === 1 ? logs[0] : null
})

const pointsSummary = computed(() => displayResult.value?.points ?? null)
const pointsRows = computed(() => pointsSummary.value?.players ?? [])

const groupDps = computed(() => {
  const players = displayResult.value?.players ?? []
  return Math.round(players.reduce((s: number, p: any) => s + (Number(p.dps) || 0), 0))
})

const avgQuick = computed(() => {
  const players = (displayResult.value?.players ?? []).filter((p: any) => Number(p.quicknessDuration) > 0)
  if (players.length === 0) {
    return 0
  }
  return players.reduce((s: number, p: any) => s + (Number(p.quicknessDuration) || 0), 0) / players.length
})

const avgAlac = computed(() => {
  const players = (displayResult.value?.players ?? []).filter((p: any) => Number(p.alacDuration) > 0)
  if (players.length === 0) {
    return 0
  }
  return players.reduce((s: number, p: any) => s + (Number(p.alacDuration) || 0), 0) / players.length
})

const avgQuickGen = computed(() => {
  const players = (displayResult.value?.players ?? []).filter((p: any) => Number(p.quicknessGenGroup) > 0)
  if (players.length === 0) {
    return 0
  }
  return players.reduce((s: number, p: any) => s + (Number(p.quicknessGenGroup) || 0), 0) / players.length
})

const avgAlacGen = computed(() => {
  const players = (displayResult.value?.players ?? []).filter((p: any) => Number(p.alacGenGroup) > 0)
  if (players.length === 0) {
    return 0
  }
  return players.reduce((s: number, p: any) => s + (Number(p.alacGenGroup) || 0), 0) / players.length
})

const wvwWinLoss = computed(() => {
  const timeline = displayResult.value?.timeline ?? []
  let wins = 0
  let losses = 0
  for (const fight of timeline) {
    const players = fight.players ?? []
    const kills = players.reduce((s: number, p: any) => s + (Number(p.kills) || 0), 0)
    const deaths = players.reduce((s: number, p: any) => s + (Number(p.deaths) || 0), 0)
    if (kills > deaths) {
      wins++
    } else {
      losses++
    }
  }
  return { wins, losses }
})

const pveKillsWipes = computed(() => {
  const logs = displayResult.value?.logs ?? []
  let kills = 0
  let wipes = 0
  for (const log of logs) {
    if (log.fightType === 0) {
      continue
    }
    if (log.isSuccess) {
      kills++
    } else {
      wipes++
    }
  }
  return { kills, wipes }
})

const wvwTotals = computed(() => {
  const players = displayResult.value?.players ?? []
  const sum = (field: string) => players.reduce((s: number, p: any) => s + (Number(p[field]) || 0), 0)
  return {
    damage: sum('damage'),
    damageTaken: sum('damageTaken'),
    kills: sum('kills'),
    downs: sum('downs'),
    deaths: sum('deaths'),
    timesDowned: sum('timesDowned'),
  }
})

type AggMode = 'sum' | 'avg'

const aggregateGroup = (players: any[], spec: Record<string, AggMode>) => {
  const result: Record<string, number> = {}
  for (const field of Object.keys(spec)) {
    const mode = spec[field]
    const values = players.map((p: any) => Number(p[field]) || 0)
    const total = values.reduce((s: number, v: number) => s + v, 0)
    result[field] = mode === 'sum' ? total : (values.length > 0 ? total / values.length : 0)
  }
  return result
}

const buildSummary = (players: any[], spec: Record<string, AggMode>) => {
  if (!players || players.length === 0) {
    return [] as any[]
  }
  const bySub = new Map<number, any[]>()
  for (const p of players) {
    const sg = Number(p.subGroup ?? 0)
    if (!bySub.has(sg)) {
      bySub.set(sg, [])
    }
    bySub.get(sg)!.push(p)
  }
  const subs = [...bySub.keys()].sort((a, b) => a - b)
  const rows: any[] = subs.map(sg => ({
    key: `sub-${sg}`,
    subGroupLabel: String(sg),
    rowLabel: `Sub ${sg}`,
    isTotal: false,
    ...aggregateGroup(bySub.get(sg)!, spec),
  }))
  rows.push({
    key: 'total',
    subGroupLabel: '',
    rowLabel: 'Total',
    isTotal: true,
    ...aggregateGroup(players, spec),
  })
  return rows
}

const fmtN = (v: number) => Math.round(v ?? 0).toLocaleString()
const fmtPct = (v: number) => `${(v ?? 0).toFixed(2)}%`
const fmtDec = (v: number, d = 2) => (v ?? 0).toFixed(d)
const fmtSec = (v: number) => ((v ?? 0) / 1000).toFixed(1)
const formatPointValue = (v: number) => Number(v ?? 0).toLocaleString(undefined, { maximumFractionDigits: 3 })

const playstyleLabels: Record<string, string> = {
  dps: 'DPS',
  'boon-dps': 'Boon DPS',
  'boon-healer': 'Boon Healer',
  mechanic: 'Mechanic',
  'support-dps': 'Support DPS',
  support: 'Support',
  'heal-support': 'Heal Support',
}

const playstyleKeyFromRow = (row: any) => {
  const breakdown = row?.playstyleBreakdown ?? []
  if (breakdown.length === 1) {
    return breakdown[0].key as string
  }
  const raw = String(row?.playstyle ?? '')
  if (playstyleLabels[raw]) {
    return raw
  }
  const match = Object.entries(playstyleLabels).find(([, label]) => label === raw)
  return match?.[0] ?? raw
}

const playstyleLabel = (row: any) => {
  const raw = String(row?.playstyle ?? '')
  if (raw === 'Mixed') {
    return raw
  }
  const key = playstyleKeyFromRow(row)
  return playstyleLabels[key] ?? raw
}

const playstyleTooltip = (row: any) => {
  const breakdown = row?.playstyleBreakdown ?? []
  if (breakdown.length <= 1) {
    return null
  }
  return breakdown.map((r: any) => `${r.count} ${r.label}`).join('\n')
}

const playstyleSeverity = (row: any) => {
  const key = playstyleKeyFromRow(row)
  if (String(row?.playstyle ?? '') === 'Mixed') {
    return 'secondary'
  }
  if (key === 'boon-healer' || key === 'heal-support') {
    return 'info'
  }
  if (key === 'boon-dps' || key === 'support-dps') {
    return 'success'
  }
  if (key === 'support') {
    return 'warn'
  }
  if (key === 'mechanic') {
    return 'contrast'
  }
  return 'secondary'
}

const damageWvwSummary = computed(() => buildSummary(displayResult.value?.players ?? [], {
  damage: 'sum', damageDownContribution: 'sum', kills: 'sum', downs: 'sum',
}))
const damagePveSummary = computed(() => buildSummary(displayResult.value?.players ?? [], {
  dps: 'sum', cleaveDps: 'sum', quicknessDuration: 'avg', alacDuration: 'avg',
}))
const supportWvwSummary = computed(() => buildSummary(displayResult.value?.players ?? [], {
  healing: 'sum', cleanses: 'sum', strips: 'sum', barrierGenerated: 'sum',
  stabOnGroup: 'avg', stabOffGroup: 'avg',
  quicknessGenGroup: 'avg', alacGenGroup: 'avg',
  interrupts: 'sum', numberOfBoonsRipped: 'sum', quicknessDuration: 'avg',
}))
const supportPveSummary = computed(() => buildSummary(displayResult.value?.players ?? [], {
  healing: 'sum', barrierGenerated: 'sum', cleanses: 'sum', strips: 'sum',
  stabOnGroup: 'avg', stabOffGroup: 'avg', quicknessGenGroup: 'avg', alacGenGroup: 'avg',
}))
const survivabilityWvwSummary = computed(() => buildSummary(displayResult.value?.players ?? [], {
  deaths: 'sum', timesDowned: 'sum', firstToDie: 'sum', damageTaken: 'sum',
  barrierMitigation: 'sum', resurrectionTime: 'sum', timesInterrupted: 'sum',
  distanceFromTag: 'avg',
}))
const survivabilityPveSummary = computed(() => buildSummary(displayResult.value?.players ?? [], {
  deaths: 'sum', timesDowned: 'sum', firstToDie: 'sum', damageTaken: 'sum',
  resurrectionTime: 'sum',
}))

const selectedPlayers = computed(() =>
  (displayResult.value?.players ?? []).filter((p: any) => !unselectedAccounts.value.has(p.accountName))
)

const selectedAccountSet = computed(() => {
  return new Set(selectedPlayers.value.map((p: any) => p.accountName))
})

const onSelectionChange = (newSelection: any[]) => {
  const selectedNames = new Set(newSelection.map((p: any) => p.accountName))
  const next = new Set<string>()
  for (const p of (displayResult.value?.players ?? [])) {
    if (!selectedNames.has(p.accountName)) {
      next.add(p.accountName)
    }
  }
  unselectedAccounts.value = next
}

const selectAllPlayers = () => {
  unselectedAccounts.value = new Set()
}

const unselectAllPlayers = () => {
  unselectedAccounts.value = new Set((displayResult.value?.players ?? []).map((p: any) => p.accountName))
}

const loadInitial = async () => {
  pending.value = true
  try {
    const r = await props.fetchAggregate()
    result.value = r
    displayResult.value = r
  } catch {
    result.value = null
    displayResult.value = null
  } finally {
    pending.value = false
  }
}

await loadInitial()

watch(() => props.reloadKey, () => {
  loadInitial()
})

const filteredAggLogs = computed(() => {
  let logs = result.value?.logs ?? []
  if (aggSuccessFilter.value === 'kills') {
    logs = logs.filter((l: any) => l.isSuccess)
  } else if (aggSuccessFilter.value === 'wipes') {
    logs = logs.filter((l: any) => !l.isSuccess)
  }
  if (aggDifficultyFilter.value !== null) {
    logs = logs.filter((l: any) => l.fightMode === aggDifficultyFilter.value)
  }
  return logs
})

watch(filteredAggLogs, async (filtered) => {
  if (!result.value) {
    return
  }
  const allIds = (result.value.logs ?? []).map((l: any) => l.fightLogId)
  const filteredIds = filtered.map((l: any) => l.fightLogId)
  if (filteredIds.length === allIds.length) {
    displayResult.value = result.value
    return
  }
  if (filteredIds.length === 0) {
    displayResult.value = null
    return
  }
  filterPending.value = true
  try {
    displayResult.value = await props.fetchAggregate(filteredIds)
  } catch {
    displayResult.value = null
  } finally {
    filterPending.value = false
  }
})

const rowActionTooltip = computed(() => props.rowAction === 'select' ? 'Show this log above' : 'View log details')

const onRowAction = (row: any) => {
  if (props.rowAction === 'select') {
    emit('select-log', row.fightLogId)
  } else {
    navigateTo(`/logs/${row.fightLogId}`)
  }
}

const onLogRowClick = (event: any) => {
  if (props.rowAction === 'select') {
    emit('select-log', event.data.fightLogId)
  }
}

const onPointLogClick = (fightLogId: number) => {
  if (props.rowAction === 'select') {
    emit('select-log', fightLogId)
  } else {
    navigateTo(`/logs/${fightLogId}`)
  }
}

const logRowStyle = (row: any) => {
  if (props.selectedLogId && row.fightLogId === props.selectedLogId) {
    return { background: 'color-mix(in srgb, var(--p-primary-color) 12%, transparent)' }
  }
  return undefined
}

const PALETTE = [
  '#6366f1', '#22d3ee', '#f59e0b', '#10b981', '#ef4444',
  '#a855f7', '#f97316', '#14b8a6', '#ec4899', '#84cc16',
  '#3b82f6', '#e11d48', '#8b5cf6', '#06b6d4', '#f43f5e',
  '#0ea5e9', '#d946ef', '#65a30d', '#fb923c', '#2dd4bf',
]
const playerColor = (i: number) => PALETTE[i % PALETTE.length]

const accountColorMap = computed(() => {
  const accounts = ((displayResult.value?.players ?? []) as any[])
    .map((p: any) => p.accountName as string)
    .filter((a, i, arr) => arr.indexOf(a) === i)
    .sort()
  const map = new Map<string, string>()
  accounts.forEach((a, i) => map.set(a, playerColor(i)))
  return map
})

const colorFor = (account: string) => accountColorMap.value.get(account) ?? playerColor(0)

const chartLabels = computed(() =>
  (displayResult.value?.timeline ?? []).map((t: any) => {
    const time = new Date(t.fightStart).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    return `${fightName(t.fightType)} ${time}`
  })
)

const allAccounts = computed(() => {
  const seen = new Set<string>()
  for (const fight of (displayResult.value?.timeline ?? [])) {
    for (const p of fight.players) {
      seen.add(p.accountName)
    }
  }
  return [...seen].filter(a => selectedAccountSet.value.has(a))
})

const makeDataset = (account: string, _i: number, getValue: (p: any) => number | null, dashed = false) => ({
  label: account,
  data: (displayResult.value?.timeline ?? []).map((fight: any) => {
    const p = fight.players.find((pl: any) => pl.accountName === account)
    return p ? getValue(p) : null
  }),
  borderColor: colorFor(account),
  backgroundColor: dashed ? 'transparent' : colorFor(account) + '22',
  tension: 0.3,
  spanGaps: true,
  pointRadius: 3,
  pointHoverRadius: 5,
  borderDash: dashed ? [5, 4] : [],
  fill: !dashed,
})

const makeBarData = (getValue: (p: any) => number | null) => {
  const players = ((displayResult.value?.players ?? []) as any[])
    .filter((p: any) => selectedAccountSet.value.has(p.accountName))
    .map((p: any) => ({ account: p.accountName as string, value: Number(getValue(p) ?? 0) }))
    .filter((p: { account: string; value: number }) => p.value !== 0)
    .sort((a: { account: string; value: number }, b: { account: string; value: number }) => b.value - a.value)
  return {
    labels: players.map((p: { account: string; value: number }) => p.account),
    datasets: [{
      data: players.map((p: { account: string; value: number }) => p.value),
      backgroundColor: players.map((p: { account: string; value: number }) => colorFor(p.account) + 'cc'),
      borderColor: players.map((p: { account: string; value: number }) => colorFor(p.account)),
      borderWidth: 1,
    }],
  }
}

const hasNonZero = (account: string, getValue: (p: any) => number | null) => {
  for (const fight of (displayResult.value?.timeline ?? [])) {
    const p = fight.players.find((pl: any) => pl.accountName === account)
    if (p && Number(getValue(p) ?? 0) !== 0) {
      return true
    }
  }
  return false
}

const chartData = (getValue: (p: any) => number | null, dashed = false) => computed(() => {
  if (props.hideLogsTab) {
    return makeBarData(getValue)
  }
  const accounts = allAccounts.value.filter(a => hasNonZero(a, getValue))
  const datasets = accounts.map((a, i) =>
    dashed ? { ...makeDataset(a, i, getValue, true), fill: false } : makeDataset(a, i, getValue)
  )
  return { labels: chartLabels.value, datasets }
})

const chartHasData = (data: any) => {
  const datasets = data?.datasets ?? []
  if (datasets.length === 0) {
    return false
  }
  return datasets.some((ds: any) => (ds.data ?? []).some((v: any) => v !== null && v !== undefined && Number(v) !== 0))
}

const wvwDamageChartData = chartData(p => p.damage)
const killsChartData = chartData(p => p.kills)
const downsChartData = chartData(p => p.downs)
const wvwDdcChartData = chartData(p => p.damageDownContribution)
const wvwBoonsRippedChartData = chartData(p => p.numberOfBoonsRipped)
const healingChartData = chartData(p => p.healing)
const barrierGenChartData = chartData(p => p.barrierGenerated)
const cleansesChartData = chartData(p => p.cleanses)
const stripsChartData = chartData(p => p.strips)
const stabOnChartData = chartData(p => p.stabOnGroup)
const stabOffChartData = chartData(p => p.stabOffGroup)
const interruptsChartData = chartData(p => p.interrupts)
const barrierMitChartData = chartData(p => p.barrierMitigation)
const resTimeChartData = chartData(p => p.resurrectionTime ? p.resurrectionTime / 1000 : 0)
const firstToDieChartData = chartData(p => p.firstToDie)
const timesInterruptedChartData = chartData(p => p.timesInterrupted)
const distanceFromTagChartData = chartData(p => p.distanceFromTag)

const tagRadialRings = [
  { r: 50, label: '300' },
  { r: 100, label: '600' },
  { r: 150, label: '900' },
  { r: 180, label: '1100' },
]

const hoveredAccount = ref<string | null>(null)
const radialTooltip = ref<{ account: string; distance: number; left: number; top: number } | null>(null)

const onRadialEnter = (pt: { account: string; distance: number }, ev: MouseEvent) => {
  hoveredAccount.value = pt.account
  const wrap = (ev.currentTarget as SVGElement).closest('.tag-radial-wrap') as HTMLElement | null
  if (!wrap) {
    return
  }
  const wrapRect = wrap.getBoundingClientRect()
  radialTooltip.value = {
    account: pt.account,
    distance: pt.distance,
    left: ev.clientX - wrapRect.left + 12,
    top: ev.clientY - wrapRect.top + 12,
  }
}

const onRadialLeave = () => {
  hoveredAccount.value = null
  radialTooltip.value = null
}

const tagRadialPoints = computed(() => {
  const players = (displayResult.value?.players ?? [])
    .filter((p: any) => selectedAccountSet.value.has(p.accountName) && Number(p.distanceFromTag) > 0)
    .map((p: any) => ({ account: p.accountName as string, distance: Number(p.distanceFromTag) }))
    .sort((a, b) => a.distance - b.distance)

  if (players.length === 0) {
    return []
  }

  const maxR = 180
  const maxDist = 1100
  return players.map((p, i) => {
    const angle = (i / players.length) * Math.PI * 2 - Math.PI / 2
    const r = Math.min(p.distance / maxDist, 1) * maxR
    const x = 200 + Math.cos(angle) * r
    const y = 200 + Math.sin(angle) * r
    const labelOffset = 14
    const labelX = 200 + Math.cos(angle) * (r + labelOffset)
    const labelY = 200 + Math.sin(angle) * (r + labelOffset)
    const anchor = labelX < 195 ? 'end' : labelX > 205 ? 'start' : 'middle'
    const shortAccount = p.account.replace(/\.\d+$/, '')
    return {
      account: p.account,
      shortAccount,
      distance: p.distance,
      x,
      y,
      labelX,
      labelY,
      anchor,
      color: colorFor(p.account),
    }
  })
})
const wvwQuickChartData = chartData(p => p.quicknessDuration, true)
const wvwQuickGenChartData = chartData(p => p.quicknessGenGroup, true)
const wvwAlacGenChartData = chartData(p => p.alacGenGroup, true)

const pveDpsChartData = chartData(p => p.dps)
const pveCleaveDpsChartData = chartData(p => p.cleaveDps)
const pveAlacChartData = chartData(p => p.alacDuration, true)
const pveQuickChartData = chartData(p => p.quicknessDuration, true)
const pveQuickGenChartData = chartData(p => p.quicknessGenGroup, true)
const pveAlacGenChartData = chartData(p => p.alacGenGroup, true)

const deathsChartData = chartData(p => p.deaths)
const downedChartData = chartData(p => p.timesDowned)
const damageTakenChartData = chartData(p => p.damageTaken)

const lineTooltipOpts: any = {
  callbacks: {
    label: (ctx: any) => `${ctx.dataset.label}: ${ctx.parsed.y?.toLocaleString() ?? 'n/a'}`,
    footer: () => [props.rowAction === 'select' ? 'Click to show above' : 'Click to open log'],
  },
  itemSort: (a: any, b: any) => (b.parsed.y ?? 0) - (a.parsed.y ?? 0),
}

const barTooltipOpts: any = {
  callbacks: {
    label: (ctx: any) => ctx.parsed.x?.toLocaleString() ?? 'n/a',
  },
}

const handleChartClick = (_event: any, elements: any[]) => {
  if (!elements.length) {
    return
  }
  const timeline = displayResult.value?.timeline ?? []
  const fight = timeline[elements[0].index]
  if (!fight?.fightLogId) {
    return
  }
  if (props.rowAction === 'select') {
    emit('select-log', fight.fightLogId)
  } else {
    window.open(`/logs/${fight.fightLogId}`, '_blank', 'noopener')
  }
}

const lineOptions = (stepSize?: number) => ({
  responsive: true,
  maintainAspectRatio: false,
  interaction: { mode: 'index' as const, intersect: false },
  onClick: handleChartClick,
  plugins: {
    legend: {
      position: 'bottom' as const,
      labels: { color: '#a1a1aa', boxWidth: 10, padding: 8, font: { size: 10 } },
    },
    tooltip: lineTooltipOpts,
  },
  scales: {
    x: { ticks: { color: '#a1a1aa', maxRotation: 25, font: { size: 10 } }, grid: { color: '#27272a' } },
    y: { ticks: { color: '#a1a1aa', font: { size: 10 }, ...(stepSize ? { stepSize } : {}) }, grid: { color: '#27272a' }, beginAtZero: true },
  },
})

const barOptions = (stepSize?: number) => ({
  indexAxis: 'y' as const,
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: { display: false },
    tooltip: barTooltipOpts,
  },
  scales: {
    x: { ticks: { color: '#a1a1aa', font: { size: 10 }, ...(stepSize ? { stepSize } : {}) }, grid: { color: '#27272a' }, beginAtZero: true },
    y: { ticks: { color: '#a1a1aa', font: { size: 10 }, autoSkip: false }, grid: { color: '#27272a' } },
  },
})

const chartType = computed(() => props.hideLogsTab ? 'bar' : 'line')
const clickableChartOptions = computed(() => props.hideLogsTab ? barOptions() : lineOptions())
const clickableIntChartOptions = computed(() => props.hideLogsTab ? barOptions(1) : lineOptions(1))

const openFights = ref(new Set<string>())
const toggleFight = (key: string) => {
  if (openFights.value.has(key)) {
    openFights.value.delete(key)
  } else {
    openFights.value.add(key)
  }
  openFights.value = new Set(openFights.value)
}

const mechanicsByGroup = computed(() =>
  groupByFightType<{
    fightType: number
    mechanicNames: string[]
    players: { accountName: string; counts: Record<string, number> }[]
  }>(displayResult.value?.mechanics ?? [])
)

const flattenMechanicPlayers = (players: { accountName: string; counts: Record<string, number> }[]) =>
  players.map(p => ({ accountName: p.accountName, ...p.counts }))

defineExpose({ reload: loadInitial })
</script>

<style scoped>
.mb-section {
  margin-bottom: 0.5rem;
}
.tabs-with-toggles {
  position: relative;
}
.tab-toggles {
  position: absolute;
  top: 0.5rem;
  right: 0.75rem;
  display: flex;
  gap: 0.5rem;
  align-items: flex-start;
  z-index: 1;
  pointer-events: auto;
}
@media (max-width: 640px) {
  .tab-toggles {
    position: static;
    margin-bottom: 0.5rem;
  }
}
.charts-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.75rem;
  margin-top: 0.75rem;
  margin-bottom: 0.75rem;
}
@media (max-width: 640px) {
  .charts-row {
    grid-template-columns: 1fr;
    gap: 0.5rem;
  }
  .chart-container {
    padding: 0.5rem;
  }
  .chart-container :deep(canvas) {
    height: 200px !important;
  }
  .chart-label {
    font-size: 0.7rem;
  }
}
.chart-container {
  background: var(--p-surface-card);
  border: 1px solid var(--p-surface-border);
  border-radius: 0.5rem;
  padding: 0.75rem;
}
.clickable-chart {
  cursor: pointer;
}
.chart-label {
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--p-text-muted-color);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  margin-bottom: 0.5rem;
}
.chart-container :deep(canvas) {
  width: 100% !important;
  height: 300px !important;
}
.mechanic-group {
  margin-top: 0.25rem;
}

.points-tab {
  display: grid;
  gap: 1rem;
}

.points-overview {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  align-items: stretch;
}

.point-component-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
}

.point-log-stack {
  display: grid;
  gap: 0.35rem;
}

.point-log-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  width: 100%;
  border: 1px solid var(--p-surface-border);
  border-radius: 6px;
  background: var(--p-surface-ground);
  color: var(--p-text-color);
  padding: 0.35rem 0.5rem;
  cursor: pointer;
  text-align: left;
}

.point-log-row:hover {
  border-color: var(--p-primary-color);
}

.point-log-row strong {
  color: var(--p-primary-color);
  white-space: nowrap;
}

.points-muted {
  color: var(--p-text-muted-color);
  font-size: 0.8rem;
}

.mechanic-group :deep(.collapsible-section) {
  margin-top: 0.5rem;
}
.mechanic-group :deep(.collapsible-title) {
  font-size: 0.875rem;
}
.mechanic-fight-wrap {
  margin: 0.25rem 0 0 1rem;
}
.mechanic-fight-toggle {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  padding: 0.4rem 0;
  margin-top: 0.25rem;
  background: none;
  border: none;
  border-bottom: 1px solid var(--p-surface-border);
  cursor: pointer;
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--p-text-color);
  gap: 0.5rem;
}
.mechanic-fight-toggle:hover {
  color: var(--p-primary-color);
}
.mechanic-fight-icon {
  font-size: 0.7rem;
  color: var(--p-text-muted-color);
  flex-shrink: 0;
}
.mechanic-table {
  margin: 0.4rem 0 0.75rem;
  border: 1px solid var(--p-surface-border);
  border-radius: 0.375rem;
  overflow: hidden;
}
.mech-zero {
  color: var(--p-text-muted-color);
}
.tag-radial-wrap {
  position: relative;
}
.tag-radial {
  width: 100%;
  height: 320px;
  display: block;
}
.tag-radial-dot {
  cursor: pointer;
  transition: r 0.1s ease;
}
.tag-radial-tooltip {
  position: absolute;
  pointer-events: none;
  background: rgba(15, 15, 20, 0.95);
  border: 1px solid var(--p-primary-color);
  border-radius: 4px;
  padding: 0.35rem 0.6rem;
  font-size: 0.75rem;
  color: var(--p-text-color);
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  z-index: 5;
  white-space: nowrap;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.4);
}
.tag-radial-tooltip strong {
  color: var(--p-primary-color);
}
.tag-radial-ring {
  fill: none;
  stroke: #3f3f46;
  stroke-width: 1;
  stroke-dasharray: 3 4;
}
.tag-radial-ring-label {
  fill: var(--p-text-muted-color);
  font-size: 9px;
  text-anchor: middle;
  font-family: inherit;
}
.tag-radial-center {
  fill: var(--p-primary-color);
}
.tag-radial-center-label {
  fill: var(--p-primary-color);
  font-size: 10px;
  font-weight: 700;
  text-anchor: middle;
  font-family: inherit;
  letter-spacing: 0.05em;
}
.tag-radial-label {
  fill: var(--p-text-color);
  font-size: 10px;
  font-family: inherit;
  dominant-baseline: middle;
}
.enemy-pane :deep(.stat-card.p-card),
.enemy-pane :deep(.stat-card.p-card .p-card-body) {
  background: color-mix(in srgb, var(--p-surface-card) 60%, var(--p-primary-color) 40%) !important;
}
.enemy-pane :deep(.stat-card.p-card) {
  border: 1px solid var(--p-primary-color) !important;
}
:deep(tfoot tr.summary-total td) {
  font-weight: 700;
  border-top: 2px solid var(--p-surface-border);
}
:deep(tfoot td) {
  font-size: 0.8rem;
  background: color-mix(in srgb, var(--p-surface-card) 60%, transparent);
}
</style>
