/**
 * 画面切り替え（袋詰指示書・ラベル管理 ⇔ 汁仕分表）
 */
document.addEventListener('DOMContentLoaded', () => {
    const menuBtns = document.querySelectorAll('.menu-btn');
    const screens = document.querySelectorAll('.screen');

    menuBtns.forEach(btn => {
        btn.addEventListener('click', async () => {
            const targetId = btn.dataset.screen;
            if (!targetId) return;

            const wasBagging = document.getElementById('screen-bagging')?.classList.contains('active');
            if (wasBagging && targetId !== 'bagging') {
                const { closeBaggingRegistration } = await import('./bagging_registration.js');
                closeBaggingRegistration();
            }

            menuBtns.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');

            screens.forEach(screen => {
                if (screen.id === `screen-${targetId}`) {
                    screen.classList.add('active');
                } else {
                    screen.classList.remove('active');
                }
            });
        });
    });
});
