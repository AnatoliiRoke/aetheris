import io

def replace_line(path, prefix, new_text):
    s = io.open(path, encoding='utf-8').read()
    i = s.index(prefix)
    j = s.index('\n', i)
    s = s[:i] + new_text + s[j:]
    io.open(path, 'w', encoding='utf-8').write(s)
    print('ok', path)


# --- 05: прибрати розділ 3.10а про «планета зникла» ---
p = 'documentation/game-design/05-flot-ta-korabli.md'
s = io.open(p, encoding='utf-8').read()
i = s.index('### 3.10а. Кораблі не зникають разом із планетою')
j = s.index('### 3.11. Звідки беруться деталі та екіпаж')
s = s[:i] + s[j:]
io.open(p, 'w', encoding='utf-8').write(s)
print('ok 05 (removed 3.10a)')

# --- 00037: прибрати розділ 12б ---
p = 'documentation/tasks/00037-pytannia.md'
s = io.open(p, encoding='utf-8').read()
i = s.index('## 12б. ✅ Планети не втрачаються')
j = s.index('## 12в. ✅ Рівень верфі відкриває корпуси')
s = s[:i] + s[j:]
io.open(p, 'w', encoding='utf-8').write(s)
print('ok 37 (removed 12б)')

# --- 00039: рішення 9 ---
replace_line('documentation/tasks/00039-skladannia-korabliv.md',
 '9. **Планети не втратаються**:',
 '9. **Планети не знищуються, не зникають і не віддаються** — такої механіки в грі немає (рішення автора, 04.11.2026). Тому окремих випадків «корабель без планети» не буває: у кожного корабля є планети, де він стоїть.')

# --- 00039: питання 4 ---
replace_line('documentation/tasks/00039-skladannia-korabliv.md',
 '4. ❓ **Планети не втратаються** — рішення автора. Лишилося уточнити: **куди переходить корабель**, якщо планети, де він стояв, більше немає (до флоту? залишається «осиротілим»? повертається на іншу планету цивілізації?).',
 '4. ✅ **Планети не знищуються, не зникають і не віддаються** — рішення автора, 04.11.2026. Сценарію «корабель без планети» не буває.')

# --- 00040: рішення 8 ---
replace_line('documentation/tasks/00040-formuvannia-flotiv.md',
 '8. **Кораблі, екіпаж і деталі — з планети**,',
 '8. **Кораблі, екіпаж і деталі — з планети**, де стоять; переміщення флоту між планетами — окрема задача (перельоти). **Планети не знищуються і не зникають**, тож у кожного корабля є планети, де він стоїть.')

# --- 00040: прибрати речення про «планета не втратається» із пункту 10 ---
p = 'documentation/tasks/00040-formuvannia-flotiv.md'
s = io.open(p, encoding='utf-8').read()
i = s.index('10. **Планети не втратаються**')
j = s.index('\n', i)
s = s[:i] + s[j+1:]
io.open(p, 'w', encoding='utf-8').write(s)
print('ok 40 (removed item 10)')

# --- 00040: прибрати питання 4а ---
p = 'documentation/tasks/00040-formuvannia-flotiv.md'
s = io.open(p, encoding='utf-8').read()
i = s.index('4а. ❓ **Корабель без планети**')
j = s.index('\n', i)
s = s[:i] + s[j+1:]
io.open(p, 'w', encoding='utf-8').write(s)
print('ok 40 (removed q4a)')
