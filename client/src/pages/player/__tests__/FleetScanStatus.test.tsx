import { screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { renderPanel, sampleFlightPlan } from '../../../test/renderPage';
import { FleetScanStatus } from '../fleets/FleetScanStatus';

describe('fleet scan status', () => {
  it('shows the remaining time of the active scan', () => {
    const now = new Date().toISOString();
    const order = { ...sampleFlightPlan().orders[0]!, typeCode: 'scan', durationSeconds: 600, parameters: JSON.stringify({ StartedAt: now }) };
    renderPanel(<FleetScanStatus order={order} clock={{ gameNow: now, timeMultiplier: 1, receivedAt: Date.now() }} onDone={vi.fn()} />);
    expect(screen.getByText(/Сканує · залишилося/)).toBeInTheDocument();
  });
});
