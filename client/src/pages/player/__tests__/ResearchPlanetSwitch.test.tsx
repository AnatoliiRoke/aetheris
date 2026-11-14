import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { fakeAuthApi, fakeResearchApi, renderPage, sampleResearchTree, signedInMe } from '../../../test/renderPage';

const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

/**
 * The level of a technology belongs to the body that researched it (decision of 04.11.2026): the screen of
 * the research shows the levels of the body chosen in its field «Планета».
 */
describe('ResearchPage: the switch of the body', () => {
  it('reads the tree of the body chosen in the field and shows its own levels', async () => {
    const user = userEvent.setup();
    const treeOf = (planetId: number | undefined) => {
      const selected = planetId === 11 ? 11 : 10;
      const base = sampleResearchTree();

      return sampleResearchTree({
        selectedPlanetId: selected,
        technologies: [{ ...base.technologies[0], learnedLevel: selected === 10 ? 5 : 0, status: selected === 10 ? 'learned' : 'available' }],
      });
    };
    const tree = vi.fn(async (planetId?: number) => treeOf(planetId));

    renderPage('/planet/research', { auth: signedIn(), research: fakeResearchApi({ tree }) });

    expect(await screen.findByText('Вивчено · рівень 5')).toBeInTheDocument();

    await user.selectOptions(screen.getByRole('combobox', { name: 'Планета дослідження' }), '11');

    expect(tree).toHaveBeenLastCalledWith(11);
    expect(await screen.findByRole('combobox', { name: 'Планета дослідження' })).toHaveValue('11');
    expect(screen.queryByText('Вивчено · рівень 5')).not.toBeInTheDocument();
  });
});
