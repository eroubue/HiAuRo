import { defineComponent, h } from 'vue';

export const ShapesSymbolMinus = defineComponent({
  name: 'ShapesSymbolMinus',
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
        h('path', {"d": "M2.3 5C2.3 5.44183 2.65818 5.8 3.1 5.8H6.9C7.34183 5.8 7.7 5.44183 7.7 5C7.7 4.55818 7.34183 4.2 6.9 4.2H3.1C2.65818 4.2 2.3 4.55818 2.3 5Z", "fillRule": "evenodd"})
      ]
    );
  }
});
