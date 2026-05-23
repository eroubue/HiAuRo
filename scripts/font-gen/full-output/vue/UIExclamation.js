import { defineComponent, h } from 'vue';

export const UIExclamation = defineComponent({
  name: 'UIExclamation',
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
        h('path', {"d": "M4.20001 7.45C4.20001 7.00817 4.55818 6.65 5.00001 6.65C5.44184 6.65 5.80001 7.00817 5.80001 7.45C5.80001 7.89183 5.44184 8.25 5.00001 8.25C4.55818 8.25 4.20001 7.89183 4.20001 7.45Z", "fillRule": "evenodd"}),
        h('path', {"d": "M4 2.65C4 2.20817 4.35817 1.75 5 1.75C5.64183 1.75 6 2.20817 6 2.65C6 3.09183 5.8 4.70817 5.7 5.15C5.6 5.59183 5.44183 5.95 5 5.95C4.55817 5.95 4.4 5.59183 4.3 5.15C4.2 4.70817 4 3.09183 4 2.65Z", "fillRule": "evenodd"})
      ]
    );
  }
});
