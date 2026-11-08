import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import {
  emptyShipyard,
  fakeAuthApi,
  fakeFleetApi,
  renderPage,
  sampleShipCatalog,
  sampleShipDesign,
  sampleShipHull,
  sampleShipModule,
  signedInMe,
} from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

/**
 * The catalog of the test: a combat hull with eight slots and a transport hull with four, and
 * the modules: a drive, a weapon of the combat, a module of another hull and a module of
 * another role. The modules are of three types, so the picker filters them.
 */
const catalog = () =>
  sampleShipCatalog({
    designLimit: 12,
    designCount: 7,
    hulls: [
      sampleShipHull({ id: 84, slots: 8 }),
      sampleShipHull({
        id: 85,
        code: 'part_85',
        nameKey: 'part_85.name',
        slots: 4,
        role: 'transport',
        partGroupCode: 'group_light',
        partGroupNameKey: 'group_light.name',
        holdCapacity: 80,
        integrity: 600,
      }),
    ],
    modules: [
      sampleShipModule({
        id: 1,
        component: 'drive',
        role: 'universal',
        energyUse: 0,
        drivePush: 260,
        partGroupCode: 'group_drives',
        partGroupNameKey: 'group_drives.name',
      }),
      sampleShipModule({
        id: 2,
        code: 'part_2',
        nameKey: 'production.part_type.part_2.name',
        component: null,
        role: 'combat',
        energyUse: 100,
        damage: 24,
        damageKind: 'energy',
        partGroupCode: 'group_weapons',
        partGroupNameKey: 'group_weapons.name',
      }),
      sampleShipModule({
        id: 3,
        code: 'part_3',
        nameKey: 'production.part_type.part_3.name',
        component: null,
        role: 'universal',
        energyUse: 0,
        compatibleHullIds: [999],
        partGroupCode: 'group_weapons',
        partGroupNameKey: 'group_weapons.name',
      }),
      sampleShipModule({
        id: 4,
        code: 'part_4',
        nameKey: 'production.part_type.part_4.name',
        component: null,
        role: 'civil',
        energyUse: 0,
        partGroupCode: 'group_civil',
        partGroupNameKey: 'group_civil.name',
        compatibleHullIds: [85],
      }),
      sampleShipModule({
        id: 5,
        code: 'part_5',
        nameKey: 'production.part_type.part_5.name',
        component: null,
        role: 'combat',
        energyUse: 0,
        partGroupCode: 'group_ammunition',
        partGroupNameKey: 'group_ammunition.name',
        isAmmunition: true,
      }),
      sampleShipModule({
        id: 6,
        code: 'part_6',
        nameKey: 'production.part_type.part_6.name',
        component: null,
        role: 'universal',
        energyUse: 0,
        partGroupCode: 'group_other',
        partGroupNameKey: 'group_other.name',
        isCapsule: true,
      }),
    ],
    moduleGroups: [
      { code: 'group_drives', nameKey: 'group_drives.name', count: 1 },
      { code: 'group_weapons', nameKey: 'group_weapons.name', count: 2 },
      { code: 'group_civil', nameKey: 'group_civil.name', count: 1 },
      { code: 'group_ammunition', nameKey: 'group_ammunition.name', count: 1 },
      { code: 'group_other', nameKey: 'group_other.name', count: 1 },
    ],
  });

/**
 * A project of the player with two slots of the same module.
 */
const design = () =>
  sampleShipDesign({
    id: 7,
    name: 'Barracuda',
    hullPartTypeId: 84,
    slots: [
      { slotIndex: 0, partTypeId: 1, markLevel: 2, hasRaceBonus: false },
      { slotIndex: 1, partTypeId: 1, markLevel: 2, hasRaceBonus: false },
      { slotIndex: 2, partTypeId: 2, markLevel: 1, hasRaceBonus: false },
    ],
    summary: {
      totalSlots: 8,
      usedSlots: 3,
      freeSlots: 5,
      weight: 357,
      energyUse: 100,
      energyPower: 120,
      crew: 96,
      holdCapacity: 300,
      integrity: 4200,
      drivePush: 520,
      energyDamage: 24,
      kineticDamage: 0,
      shieldCapacity: 0,
      hasDrive: false,
      hasReactor: true,
    },
  });

const apis = () => ({
  auth: signedIn(),
  fleet: fakeFleetApi({
    shipCatalog: vi.fn().mockResolvedValue(catalog()),
    designs: vi.fn().mockResolvedValue([design()]),
  }),
});

/**
 * The window of the design of the player: it is found by the field of the name, so the tests do
 * not depend on the name of the window.
 */
async function designWindow(): Promise<HTMLElement> {
  const field = await screen.findByLabelText('Назва проєкту');

  return field.closest('[role="dialog"]') as HTMLElement;
}

describe('the shipyard screen (task 00038)', () => {
  it('has the tabs of the design, the assembly and the dismantling', async () => {
    renderPage('/shipyard', apis());

    const tabs = await screen.findByRole('tablist', { name: 'Вкладки верфі' });

    expect(within(tabs).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Дизайни', 'Збірка', 'Розбирання']);
  });

  it('counts the designs of the player against the limit of the bureau', async () => {
    renderPage('/shipyard', apis());

    expect(await screen.findByText('Дизайни: 7 з 12')).toBeInTheDocument();
  });

  it('puts the button of a new design above and under the list of the designs', async () => {
    renderPage('/shipyard', apis());

    await screen.findByRole('heading', { name: 'Barracuda' });

    expect(screen.getAllByRole('button', { name: 'Додати новий дизайн' })).toHaveLength(2);
  });

  it('filters the designs by the class of the hull with the number of them', async () => {
    renderPage('/shipyard', apis());

    const filters = await screen.findByRole('group', { name: 'Фільтр за класом корпусу' });

    expect(within(filters).getAllByRole('button').map((chip) => chip.textContent)).toEqual(['Усі 1', 'Легкі 1', 'group_light.name 0']);
  });

  it('shows the design of the player with its hull, its modules and its numbers', async () => {
    renderPage('/shipyard', apis());

    const card = (await screen.findByRole('heading', { name: 'Barracuda' })).closest('article')!;

    expect(within(card).getByText('Корпус «production.part_type.part_84.name» · Легкі')).toBeInTheDocument();
    expect(within(card).getByText('Встановлені модулі')).toBeInTheDocument();
    // A module that stands in two slots is shown once with its mark and its number.
    expect(within(card).getByText('production.part_type.part_1.name')).toBeInTheDocument();
    expect(within(card).getByText('Mk 2')).toBeInTheDocument();
    expect(within(card).getByText('×2')).toBeInTheDocument();
    expect(within(card).getByText('357 т')).toBeInTheDocument();
    expect(within(card).getByText('3 / 8')).toBeInTheDocument();
    expect(within(card).getByText('100 / 120 МВт')).toBeInTheDocument();
    expect(within(card).getByText('96')).toBeInTheDocument();
    expect(within(card).getByText('520 кН')).toBeInTheDocument();
    expect(within(card).getByText('24 енерг. · 0 кінет.')).toBeInTheDocument();
  });

  it('marks the design that breaks a rule of the constructor', async () => {
    renderPage('/shipyard', apis());

    const card = (await screen.findByRole('heading', { name: 'Barracuda' })).closest('article')!;
    const mark = within(card).getByText('Потребує виправлення');

    expect(mark).toBeInTheDocument();
    // The mark names the rule that the design breaks.
    expect(mark.closest('span')).toHaveAttribute('title', 'Немає двигуна');
  });

  it('has the actions of the design: to edit, to send to the assembly and to archive', async () => {
    renderPage('/shipyard', apis());

    const card = (await screen.findByRole('heading', { name: 'Barracuda' })).closest('article')!;

    expect(within(card).getByRole('button', { name: 'Редагувати' })).toBeInTheDocument();
    expect(within(card).getByRole('button', { name: 'До збірки' })).toBeInTheDocument();
    expect(within(card).getByRole('button', { name: 'Архівувати' })).toBeInTheDocument();
  });

  it('archives the design of the player', async () => {
    const user = userEvent.setup();
    const fleet = fakeFleetApi({
      shipCatalog: vi.fn().mockResolvedValue(catalog()),
      designs: vi.fn().mockResolvedValue([design()]),
    });

    renderPage('/shipyard', { auth: signedIn(), fleet });

    const card = (await screen.findByRole('heading', { name: 'Barracuda' })).closest('article')!;

    await user.click(within(card).getByRole('button', { name: 'Архівувати' }));

    expect(await screen.findByText('Проєкт заархівовано.')).toBeInTheDocument();
    expect(fleet.archiveDesign).toHaveBeenCalledWith(7);
  });

  it('opens the first step of a new design with the hulls of the catalog', async () => {
    const user = userEvent.setup();
    renderPage('/shipyard', apis());

    await user.click((await screen.findAllByRole('button', { name: 'Додати новий дизайн' }))[0]);

    const dialog = await screen.findByRole('dialog', { name: 'Новий дизайн' });

    expect(within(dialog).getByText('Оберіть корпус')).toBeInTheDocument();
    expect(within(dialog).getByText('Корпус задає лише розмір і місткість — роль корабля визначають модулі.')).toBeInTheDocument();
    expect(within(dialog).getAllByRole('button', { name: 'Обрати' })).toHaveLength(2);
    expect(within(dialog).getByRole('button', { name: 'Скасувати' })).toBeInTheDocument();
  });

  it('shows the class of the hull in the cards of the first step', async () => {
    const user = userEvent.setup();
    renderPage('/shipyard', apis());

    await user.click((await screen.findAllByRole('button', { name: 'Додати новий дизайн' }))[0]);

    const dialog = await screen.findByRole('dialog', { name: 'Новий дизайн' });
    const hulls = Array.from(dialog.querySelectorAll('.sh-hull')) as HTMLElement[];

    expect(within(hulls[0]).getByText('Легкі')).toBeInTheDocument();
    expect(within(hulls[1]).getByText('group_light.name')).toBeInTheDocument();
  });

  it('opens the second step with the slots of the hull and the summary of the design', async () => {
    const user = userEvent.setup();
    renderPage('/shipyard', apis());

    await user.click((await screen.findAllByRole('button', { name: 'Додати новий дизайн' }))[0]);

    const picker = await screen.findByRole('dialog', { name: 'Новий дизайн' });

    await user.click(within(picker).getAllByRole('button', { name: 'Обрати' })[0]);

    const dialog = await designWindow();

    expect(within(dialog).getByText('Слоти корабля')).toBeInTheDocument();
    expect(within(dialog).getByText('Слоти: 0 з 8')).toBeInTheDocument();
    expect(within(dialog).getAllByText('+')).toHaveLength(8);
    expect(within(dialog).getByText('Підсумок дизайну')).toBeInTheDocument();
    expect(within(dialog).getByText('Перевірка')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Скасувати' })).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Зберегти дизайн' })).toBeDisabled();
  });

  it('shows the hull of the design with its slots, its hold and its strength', async () => {
    const user = userEvent.setup();
    renderPage('/shipyard', apis());

    await user.click((await screen.findAllByRole('button', { name: 'Додати новий дизайн' }))[0]);

    const picker = await screen.findByRole('dialog', { name: 'Новий дизайн' });

    await user.click(within(picker).getAllByRole('button', { name: 'Обрати' })[0]);

    const dialog = await designWindow();
    const head = dialog.querySelector('.sh-window__head') as HTMLElement;

    expect(within(head).getByText('production.part_type.part_84.name')).toBeInTheDocument();
    expect(within(head).getByText('Легкі')).toBeInTheDocument();
    expect(within(head).getByText('8')).toBeInTheDocument();
    expect(within(head).getByText('100 т')).toBeInTheDocument();
    expect(within(head).getByText('100')).toBeInTheDocument();
    expect(within(head).getByRole('button', { name: 'Змінити корпус' })).toBeInTheDocument();
  });

  it('names the rules that the design breaks in the check of the constructor', async () => {
    const user = userEvent.setup();
    renderPage('/shipyard', apis());

    await user.click((await screen.findAllByRole('button', { name: 'Додати новий дизайн' }))[0]);

    const picker = await screen.findByRole('dialog', { name: 'Новий дизайн' });

    await user.click(within(picker).getAllByRole('button', { name: 'Обрати' })[0]);

    const dialog = await designWindow();

    expect(within(dialog).getByText('Немає двигуна')).toBeInTheDocument();
    expect(within(dialog).getByText('Немає реактора')).toBeInTheDocument();
    expect(within(dialog).getByText('Немає реактора')).toBeInTheDocument();
  });

  it('puts a module into a slot and counts the taken slots', async () => {
    const user = userEvent.setup();
    renderPage('/shipyard', apis());

    await user.click((await screen.findAllByRole('button', { name: 'Додати новий дизайн' }))[0]);

    const picker = await screen.findByRole('dialog', { name: 'Новий дизайн' });

    await user.click(within(picker).getAllByRole('button', { name: 'Обрати' })[0]);

    const dialog = await designWindow();

    await user.click(within(dialog).getAllByRole('button', { name: /Слот 1/ })[0]);

    const modules = await screen.findByRole('dialog', { name: 'Вибір модуля' });

    await user.click(within(modules).getAllByRole('button', { name: 'Поставити' })[0]);

    await waitFor(() => expect(within(dialog).getByText('Слоти: 1 з 8')).toBeInTheDocument());
  });

  it('shows the peaceful module on the transport hull', async () => {
    const user = userEvent.setup();
    renderPage('/shipyard', apis());

    await user.click((await screen.findAllByRole('button', { name: 'Додати новий дизайн' }))[0]);

    const picker = await screen.findByRole('dialog', { name: 'Новий дизайн' });

    // The second hull of the test is a transport, and the peaceful module goes on it: the
    // transport hull is a peaceful hull.
    await user.click(within(picker).getAllByRole('button', { name: 'Обрати' })[1]);

    const dialog = await designWindow();

    await user.click(within(dialog).getAllByRole('button', { name: /Слот 1/ })[0]);

    const modules = await screen.findByRole('dialog', { name: 'Вибір модуля' });

    expect(within(modules).getByText('production.part_type.part_4.name')).toBeInTheDocument();
  });

  it('does not show the modules that do not go into the hull', async () => {
    const user = userEvent.setup();
    renderPage('/shipyard', apis());

    await user.click((await screen.findAllByRole('button', { name: 'Додати новий дизайн' }))[0]);

    const picker = await screen.findByRole('dialog', { name: 'Новий дизайн' });

    await user.click(within(picker).getAllByRole('button', { name: 'Обрати' })[0]);

    const dialog = await designWindow();

    await user.click(within(dialog).getAllByRole('button', { name: /Слот 1/ })[0]);

    const modules = await screen.findByRole('dialog', { name: 'Вибір модуля' });

    // The module of another hull and the module of another role are not shown at all.
    expect(within(modules).queryByText('production.part_type.part_3.name')).not.toBeInTheDocument();
    expect(within(modules).queryByText('production.part_type.part_4.name')).not.toBeInTheDocument();
    expect(within(modules).getByText('production.part_type.part_1.name')).toBeInTheDocument();
    expect(within(modules).getByText('production.part_type.part_2.name')).toBeInTheDocument();
  });

  it('does not show the ammunition and the capsules, they are not modules', async () => {
    const user = userEvent.setup();
    renderPage('/shipyard', apis());

    await user.click((await screen.findAllByRole('button', { name: 'Додати новий дизайн' }))[0]);

    const picker = await screen.findByRole('dialog', { name: 'Новий дизайн' });

    await user.click(within(picker).getAllByRole('button', { name: 'Обрати' })[0]);

    const dialog = await designWindow();

    await user.click(within(dialog).getAllByRole('button', { name: /Слот 1/ })[0]);

    const modules = await screen.findByRole('dialog', { name: 'Вибір модуля' });

    // The ammunition is loaded into the weapon and the capsule is used on a body: neither of
    // them is a module of a ship, so neither shows in the list.
    expect(within(modules).queryByText('production.part_type.part_5.name')).not.toBeInTheDocument();
    expect(within(modules).queryByText('production.part_type.part_6.name')).not.toBeInTheDocument();
  });

  it('filters the modules by their type', async () => {
    const user = userEvent.setup();
    renderPage('/shipyard', apis());

    await user.click((await screen.findAllByRole('button', { name: 'Додати новий дизайн' }))[0]);

    const picker = await screen.findByRole('dialog', { name: 'Новий дизайн' });

    await user.click(within(picker).getAllByRole('button', { name: 'Обрати' })[0]);

    const dialog = await designWindow();

    await user.click(within(dialog).getAllByRole('button', { name: /Слот 1/ })[0]);

    const modules = await screen.findByRole('dialog', { name: 'Вибір модуля' });
    const types = within(modules).getByRole('tablist', { name: 'Типи модулів' });

    expect(within(types).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Усі', 'group_drives.name', 'group_weapons.name']);

    await user.click(within(types).getByRole('tab', { name: 'group_drives.name' }));

    expect(within(modules).getByText('production.part_type.part_1.name')).toBeInTheDocument();
    expect(within(modules).queryByText('production.part_type.part_2.name')).not.toBeInTheDocument();
  });

  it('does not care whether the module is on the stock', async () => {
    const user = userEvent.setup();
    renderPage('/shipyard', apis());

    await user.click((await screen.findAllByRole('button', { name: 'Додати новий дизайн' }))[0]);

    const picker = await screen.findByRole('dialog', { name: 'Новий дизайн' });

    await user.click(within(picker).getAllByRole('button', { name: 'Обрати' })[0]);

    const dialog = await designWindow();

    await user.click(within(dialog).getAllByRole('button', { name: /Слот 1/ })[0]);

    const modules = await screen.findByRole('dialog', { name: 'Вибір модуля' });

    // The stock of the body is not written anywhere in the picker: a project is designed on paper.
    expect(within(modules).queryByText(/На складі/)).not.toBeInTheDocument();
  });

  it('saves the project and tells the player that it is saved', async () => {
    const user = userEvent.setup();
    const fleet = fakeFleetApi({
      shipCatalog: vi.fn().mockResolvedValue(catalog()),
      designs: vi.fn().mockResolvedValue([design()]),
    });

    renderPage('/shipyard', { auth: signedIn(), fleet });

    await user.click((await screen.findAllByRole('button', { name: 'Додати новий дизайн' }))[0]);

    const picker = await screen.findByRole('dialog', { name: 'Новий дизайн' });

    await user.click(within(picker).getAllByRole('button', { name: 'Обрати' })[0]);

    const dialog = await designWindow();

    await user.type(within(dialog).getByLabelText('Назва проєкту'), 'Перехоплювач');
    await user.click(within(dialog).getByRole('button', { name: 'Зберегти дизайн' }));

    expect(await screen.findByText('Проєкт створено.')).toBeInTheDocument();
    expect(fleet.createDesign).toHaveBeenCalledWith({
      planetId: expect.any(Number),
      name: 'Перехоплювач',
      hullPartTypeId: 84,
      hasRaceBonusSlot: false,
    });
  });

  it('assembles a project, names a closed hull and dismantles a finished ship', async () => {
    const user = userEvent.setup();
    const yard = {
      planetId: 10,
      planetBuildingId: 51,
      level: 1,
      used: 1,
      total: 2,
      gameNow: '2026-11-07T00:00:00Z',
      timeMultiplier: 1,
      orders: [
        {
          id: 3,
          designId: 7,
          title: 'Барракуда',
          position: 1,
          underWay: true,
          finishesAt: '2026-11-07T01:00:00Z',
          durationSeconds: 3600,
          costs: [],
          quantity: 1,
          producedQuantity: 0,
        },
      ],
      designs: [
        {
          designId: 7,
          name: 'Барракуда',
          hullPartTypeId: 21,
          hullNameKey: 'production.part.part_h_21.name',
          canEnqueue: true,
          reasonCode: null,
          requirementNameKey: null,
          requirementLevel: null,
          crew: 4,
          credits: 20,
          durationSeconds: 1800,
          costs: [{ resourceTypeId: 1, amount: 4, nameKey: 'world.resource.resource_30.name' }],
          parts: [
            {
              partTypeId: 21,
              nameKey: 'production.part.part_h_21.name',
              markLevel: 1,
              hasRaceBonus: false,
              quantity: 1,
              available: 2,
              isHull: true,
            },
          ],
        },
        {
          designId: 8,
          name: 'Сапсан',
          hullPartTypeId: 1,
          hullNameKey: 'production.part.part_h_1.name',
          canEnqueue: false,
          reasonCode: 'planets_requirement_building_level',
          requirementNameKey: 'planets.building.building_51.name',
          requirementLevel: 2,
          crew: 0,
          credits: 0,
          durationSeconds: 1800,
          costs: [],
          parts: [],
        },
      ],
      ships: [
        {
          id: 4,
          planetId: 10,
          designId: 7,
          name: 'Барракуда',
          hullNameKey: 'production.part.part_h_2.name',
          classNameKey: 'production.part_group.pg_2.name',
          stateCode: 'ready',
          crew: 4,
          upkeepPerHour: 0,
          modules: [],
        },
      ],
    };
    const fleet = fakeFleetApi({
      shipCatalog: vi.fn().mockResolvedValue(catalog()),
      designs: vi.fn().mockResolvedValue([design()]),
      shipyard: vi.fn().mockResolvedValue(yard),
      enqueueShipyard: vi.fn().mockResolvedValue(yard),
      disassembleShip: vi.fn().mockResolvedValue({ ...yard, ships: [] }),
    });

    renderPage('/shipyard', { auth: signedIn(), fleet });

    await user.click(await screen.findByRole('tab', { name: 'Збірка' }));

    expect(await screen.findByRole('heading', { name: 'Черга збірки' })).toBeInTheDocument();
    expect(screen.getByText('Потрібно: planets.building.building_51.name, рівень 2')).toBeInTheDocument();
    expect(screen.getByText('0/год')).toBeInTheDocument();

    const buildButtons = screen.getAllByRole('button', { name: 'Побудувати' });
    const openBuild = buildButtons.find((button) => !button.hasAttribute('disabled'));

    expect(openBuild).toBeDefined();
    await user.click(openBuild!);
    expect(fleet.enqueueShipyard).toHaveBeenCalledWith({ planetId: 10, planetBuildingId: 51, designId: 7, quantity: 1 });

    await user.click(screen.getByRole('tab', { name: 'Розбирання' }));
    await user.click(await screen.findByRole('button', { name: 'Розібрати Барракуда' }));
    expect(fleet.disassembleShip).toHaveBeenCalledWith(4);
  });

  it('shows the finished ships by the class of the hull and with how many ships each project has', async () => {
    const user = userEvent.setup();
    const yard = {
      ...emptyShipyard(10),
      planetBuildingId: 51,
      level: 1,
      ships: [
        { id: 4, planetId: 10, designId: 7, name: 'Барракуда', hullNameKey: 'production.part.part_h_2.name', classNameKey: 'production.part_group.pg_2.name', stateCode: 'ready', crew: 4, upkeepPerHour: 3, modules: [] },
        { id: 5, planetId: 10, designId: 7, name: 'Барракуда', hullNameKey: 'production.part.part_h_2.name', classNameKey: 'production.part_group.pg_2.name', stateCode: 'ready', crew: 4, upkeepPerHour: 3, modules: [] },
        { id: 6, planetId: 10, designId: 8, name: 'Жук', hullNameKey: 'production.part.part_h_21.name', classNameKey: 'production.part_group.pg_1.name', stateCode: 'ready', crew: 2, upkeepPerHour: 1, modules: [] },
      ],
    };
    const fleet = fakeFleetApi({
      shipCatalog: vi.fn().mockResolvedValue(catalog()),
      designs: vi.fn().mockResolvedValue([]),
      shipyard: vi.fn().mockResolvedValue(yard),
    });

    renderPage('/shipyard', { auth: signedIn(), fleet });

    await user.click(await screen.findByRole('tab', { name: 'Збірка' }));

    // The class of the hull is the head of a group, and the ships of a project are a count.
    expect(await screen.findByText('production.part_group.pg_2.name')).toBeInTheDocument();
    expect(screen.getByText('production.part_group.pg_1.name')).toBeInTheDocument();
    expect(screen.getByText('× 2')).toBeInTheDocument();
    expect(screen.getByText('× 1')).toBeInTheDocument();

    // The upkeep of the group is the sum of the upkeep of its ships.
    expect(screen.getByText('6/год')).toBeInTheDocument();
    expect(screen.getByText('1/год')).toBeInTheDocument();
  });
});
