/* audit.js — 移动端布局债排查工具
 * 用法：DevTools Console 里粘贴本文件内容，然后执行 auditPage()
 * 输出：表格 + 汇总 + 可复制的 JSON
 */
(function () {
    window.__audit_skippedTags = new Set([
        'SCRIPT', 'STYLE', 'LINK', 'META', 'HEAD', 'TITLE',
        'SVG', 'PATH', 'G', 'CIRCLE', 'RECT', 'LINE',
    ]);

    window.__audit_isVisible = function (el) {
        if (el.offsetParent === null && el.tagName !== 'BODY') return false;
        const cs = getComputedStyle(el);
        if (cs.display === 'none' || cs.visibility === 'hidden') return false;
        if (parseFloat(cs.opacity) === 0) return false;
        return true;
    };

    window.__audit_describe = function (el) {
        const cls = (el.className?.toString?.() || '').slice(0, 80);
        const text = (el.textContent?.trim?.() || '').slice(0, 40).replace(/\s+/g, ' ');
        return {
            tag: el.tagName,
            cls,
            text,
            width: Math.round(el.getBoundingClientRect().width),
            height: Math.round(el.getBoundingClientRect().height),
            right: Math.round(el.getBoundingClientRect().right),
            left: Math.round(el.getBoundingClientRect().left),
        };
    };

    // ───── 维度 1：横向溢出 ─────
    window.__audit_overflow = function () {
        const vw = window.innerWidth;
        const hits = [];
        document.querySelectorAll('body *').forEach(el => {
            if (window.__audit_skippedTags.has(el.tagName)) return;
            if (!window.__audit_isVisible(el)) return;
            // 关闭状态的抽屉/弹层靠 transform 移出屏幕，offsetParent 仍在 → 属误报
            if (el.closest('[class*="mud-drawer"][class*="closed"], .mud-popover:not(.mud-popover-open)')) return;
            // 可横向滚动容器内的溢出是设计行为（如移动端表格），不报
            const scrollAncestor = el.parentElement?.closest('[style*="overflow"]');
            if (scrollAncestor) {
                const cs = getComputedStyle(scrollAncestor);
                const ox = cs.overflowX;
                const oy = cs.overflowY;
                // overflow:hidden / auto / scroll / overlay 都会裁剪溢出，不报
                if (['auto', 'scroll', 'overlay', 'hidden'].includes(ox)
                    || ['auto', 'scroll', 'overlay', 'hidden'].includes(oy)) return;
            }
            // MudBlazor grid 用负边距抵消 gutter，属设计行为，不报
            if (el.classList.contains('mud-grid') || el.classList.contains('mud-grid-item')) return;
            const r = el.getBoundingClientRect();
            if (r.right > vw + 1 || r.left < -1) {
                hits.push(window.__audit_describe(el));
            }
        });
        // 去重：如果一个元素和它的父元素都溢出，只留最深层（子孙）
        const filtered = hits.filter(h => {
            return !hits.some(other =>
                other !== h &&
                other.cls.includes(h.cls.slice(0, 20)) &&
                other.width <= h.width
            );
        });
        return { count: hits.length, deduped: filtered, hits };
    };

    // ───── 维度 2：固定宽度元素 ─────
    window.__audit_fixedWidth = function () {
        const vw = window.innerWidth;
        const hits = [];
        document.querySelectorAll('.mud-input-control, .mud-input, [style*="min-width"], [style*="width"]').forEach(el => {
            if (!window.__audit_isVisible(el)) return;
            const cs = getComputedStyle(el);
            const minW = cs.minWidth;
            const w = cs.width;
            const styleAttr = el.getAttribute('style') || '';

            const minWVal = minW && minW !== 'none' && minW !== 'auto' ? parseFloat(minW) : 0;
            const wVal = w ? parseFloat(w) : 0;

            // 只报两类真问题：显式 min-width ≥ 70% 视口宽；或内联样式把宽度写成
            // 固定 px 值且 ≥ 60% 视口宽。min()/clamp() 等响应式内联写法不算债。
            // 注意：max-width 不是固定宽度，需用 (?<!max-) 负向后行断言排除。
            const inlineFixed = /(?<!max-)(?:min-width|width)\s*:\s*(\d+(?:\.\d+)?)px/.exec(styleAttr);
            const inlineFixedBig = inlineFixed && parseFloat(inlineFixed[1]) >= vw * 0.6;
            if ((minW && minW !== 'none' && minW !== 'auto' && minWVal >= vw * 0.7) || inlineFixedBig) {
                const d = window.__audit_describe(el);
                d.minWidth = minW;
                d.computedWidth = w;
                d.styleAttr = styleAttr.slice(0, 80);
                hits.push(d);
            }
        });
        return { count: hits.length, hits };
    };

    // ───── 维度 3：多列布局 ─────
    window.__audit_gridColumns = function () {
        const vw = window.innerWidth;
        const items = document.querySelectorAll('.mud-grid-item');
        const narrow = [];
        items.forEach(el => {
            if (!window.__audit_isVisible(el)) return;
            // 带显式响应式档位的栅格项（如 xs-6/md-3）是有意设计，不报；
            // 只报没有任何 xs 档位、窄屏下不会自动堆叠的栅格项
            if (/mud-grid-item-xs-/.test(el.className)) return;
            const r = el.getBoundingClientRect();
            if (r.width < vw * 0.6 && r.width > 0) {
                const d = window.__audit_describe(el);
                d.vwRatio = (r.width / vw).toFixed(2);
                narrow.push(d);
            }
        });
        return { total: items.length, narrowCount: narrow.length, narrow };
    };

    // ───── 维度 4：表格溢出 ─────
    window.__audit_tables = function () {
        const vw = window.innerWidth;
        const tables = document.querySelectorAll('.mud-table');
        const hits = [];
        tables.forEach(el => {
            const r = el.getBoundingClientRect();
            if (r.width > vw + 1) {
                const d = window.__audit_describe(el);
                d.vwRatio = (r.width / vw).toFixed(2);
                hits.push(d);
            }
        });
        return { total: tables.length, overflowCount: hits.length, hits };
    };

    // ───── 维度 5：工具栏/按钮组溢出 ─────
    window.__audit_toolbars = function () {
        const hits = [];
        document.querySelectorAll('.mud-stack-row, .mud-toolbar, .mud-button-group-root, .d-flex.flex-row').forEach(el => {
            if (!window.__audit_isVisible(el)) return;
            // 顶部 AppBar 全宽是设计使然，不算溢出
            if (el.classList.contains('mud-toolbar-appbar')) return;
            const r = el.getBoundingClientRect();
            const parent = el.parentElement;
            if (!parent) return;
            const pr = parent.getBoundingClientRect();
            // 仅当自身右边超出父容器才算溢出
            if (r.right > pr.right + 2) {
                const d = window.__audit_describe(el);
                d.parentWidth = Math.round(pr.width);
                hits.push(d);
            }
        });
        return { count: hits.length, hits };
    };

    // ───── 维度 6：触控目标 < 44dp ─────
    window.__audit_touchTargets = function () {
        const selectors = 'button, a[href], .mud-button-root, .mud-icon-button, .mud-nav-link, .mud-menu-item';
        const hits = [];
        document.querySelectorAll(selectors).forEach(el => {
            if (!window.__audit_isVisible(el)) return;
            // 输入框内嵌的小图标按钮（清除/ adornment）有意做小，不报
            if (el.closest('.mud-input-adornment')) return;
            const r = el.getBoundingClientRect();
            if (r.width < 44 || r.height < 44) {
                const d = window.__audit_describe(el);
                d.tooSmall = r.width < 44 ? (r.height < 44 ? 'both' : 'width') : 'height';
                // MudBlazor 标准按钮/图标按钮默认高 36px、导航链接默认高 41px，
                // 属组件库默认值，与页面级问题分开统计，避免淹没真问题
                d.isComponentDefault = (el.matches('.mud-button-root, .mud-icon-button') && r.height >= 36)
                    || el.classList.contains('mud-nav-link');
                hits.push(d);
            }
        });
        const nonDefault = hits.filter(h => !h.isComponentDefault);
        return { count: hits.length, nonDefaultCount: nonDefault.length, componentDefaultCount: hits.length - nonDefault.length, hits };
    };

    // ───── 主入口 ─────
    window.auditPage = function () {
        const vw = window.innerWidth;
        const vh = window.innerHeight;
        const url = location.pathname;
        console.clear();
        console.log(`%c[audit] ${url} @ ${vw}×${vh}px`, 'color:#0af;font-weight:bold;font-size:14px');

        const overflow = window.__audit_overflow();
        const fixedW = window.__audit_fixedWidth();
        const grid = window.__audit_gridColumns();
        const tables = window.__audit_tables();
        const toolbars = window.__audit_toolbars();
        const touch = window.__audit_touchTargets();

        const summary = {
            url,
            viewport: `${vw}×${vh}`,
            overflow: overflow.count,
            fixedWidth: fixedW.count,
            narrowGridItems: grid.narrowCount,
            tableOverflow: tables.overflowCount,
            toolbarOverflow: toolbars.count,
            smallTouchTargets: touch.nonDefaultCount,
            smallTouchComponentDefaults: touch.componentDefaultCount,
        };

        console.log('%c[汇总]', 'color:#0af;font-weight:bold');
        console.table([summary]);

        if (overflow.count) {
            console.group('%c横向溢出', 'color:#c00;font-weight:bold');
            console.table(overflow.deduped);
            console.groupEnd();
        }
        if (fixedW.count) {
            console.group('%c固定宽度元素', 'color:#c60;font-weight:bold');
            console.table(fixedW.hits);
            console.groupEnd();
        }
        if (grid.narrowCount) {
            console.group('%c多列布局（未响应式）', 'color:#c60;font-weight:bold');
            console.table(grid.narrow);
            console.groupEnd();
        }
        if (tables.overflowCount) {
            console.group('%c表格溢出', 'color:#c00;font-weight:bold');
            console.table(tables.hits);
            console.groupEnd();
        }
        if (toolbars.count) {
            console.group('%c工具栏/按钮组溢出', 'color:#c60;font-weight:bold');
            console.table(toolbars.hits);
            console.groupEnd();
        }
        if (touch.count) {
            console.group('%c触控目标 < 44dp', 'color:#c60;font-weight:bold');
            console.table(touch.hits);
            console.groupEnd();
        }

        // 供复制粘贴
        window.__audit_last = {
            summary,
            detail: { overflow, fixedW, grid, tables, toolbars, touch },
        };
        console.log('%c[提示] 完整结果已存到 window.__audit_last，可 JSON.stringify(window.__audit_last)', 'color:#888');
        return summary;
    };

    console.log('%c[audit] 已加载。执行 auditPage() 开始检测。', 'color:#0a0;font-weight:bold');
})();
