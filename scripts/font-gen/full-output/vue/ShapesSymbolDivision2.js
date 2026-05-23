import { defineComponent, h } from 'vue';

export const ShapesSymbolDivision2 = defineComponent({
  name: 'ShapesSymbolDivision2',
  props: {
    class: {
      type: String,
      default: ''
    }
  },
  setup(props, { attrs }) {
    return () => h(
      'svg',
      {
        viewBox: '0 0 20 20',
        
        class: `game-icons ${props.class}`,
        ...attrs
      },
      [
        h('path', {"d": "M3.81667 7.53766C4.2171 7.72439 4.69309 7.55114 4.87981 7.15071L6.57029 3.52548C6.75701 3.12505 6.58377 2.64906 6.18333 2.46234C5.7829 2.27561 5.30692 2.44886 5.12019 2.84929L3.42972 6.47452C3.24299 6.87495 3.41624 7.35094 3.81667 7.53766Z", "fillRule": "evenodd"})
      ]
    );
  }
});
